using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using static Confluent.Kafka.ConfigPropertyNames;

namespace Common.Logging
{
    public class KafkaProducerService : IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;
        private readonly AsyncPolicy _retryPolicy;
        private readonly AsyncPolicy _circuitBreakerPolicy;

        public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger)
        {
            _logger = logger;

            // Kafka yapılandırmasını oku
            var kafkaConfig = configuration.GetSection("Kafka").Get<IConfigurationSection>();
            var producerConfig = kafkaConfig.GetSection("Producer");

            var bootstrapServers = kafkaConfig["BootstrapServers"];
            var sslCaLocation = kafkaConfig["SslCaLocation"];
            var sslCertificateLocation = kafkaConfig["SslCertificateLocation"];
            var sslKeyLocation = kafkaConfig["SslKeyLocation"];
            var retryBackoffMs = int.Parse(producerConfig["RetryBackoffMs"]);
            var messageTimeoutMs = int.Parse(producerConfig["MessageTimeoutMs"]);

            // RetryPolicy yapılandırması
            var maxRetryAttempts = int.Parse(producerConfig["RetryPolicy:MaxRetryAttempts"]);
            var baseDelaySeconds = int.Parse(producerConfig["RetryPolicy:BaseDelaySeconds"]);

            // CircuitBreaker yapılandırması
            var failureThreshold = int.Parse(producerConfig["CircuitBreaker:FailureThreshold"]);
            var durationOfBreakSeconds = int.Parse(producerConfig["CircuitBreaker:DurationOfBreakSeconds"]);

            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                SecurityProtocol = string.IsNullOrEmpty(sslCaLocation) ? SecurityProtocol.Plaintext : SecurityProtocol.Ssl,
                SslCaLocation = sslCaLocation,
                SslCertificateLocation = sslCertificateLocation,
                SslKeyLocation = sslKeyLocation,
                RetryBackoffMs = retryBackoffMs,
                MessageTimeoutMs = messageTimeoutMs
            };

            _producer = new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, error) =>
                {
                    _logger.LogError($"Kafka Producer Error: {error.Reason}");
                })
                .Build();

            // Retry Policy
            _retryPolicy = Policy
                .Handle<ProduceException<string, string>>()
                .WaitAndRetryAsync(maxRetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(baseDelaySeconds, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} for KafkaProducer: {exception.Message}");
                    });

            // Circuit Breaker Policy
            _circuitBreakerPolicy = Policy
                .Handle<ProduceException<string, string>>()
                .CircuitBreakerAsync(failureThreshold, TimeSpan.FromSeconds(durationOfBreakSeconds),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError($"Circuit broken for KafkaProducer: {exception.Message}. Duration: {duration.TotalSeconds} seconds.");
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit reset for KafkaProducer.");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogWarning("Circuit is half-open for KafkaProducer.");
                    });

            EnsureTopicExists(config.BootstrapServers, configuration["Kafka:Topic"] ?? "default-topic").Wait();
        }

        public async Task ProduceAsync(string topic, string key, string value)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    var activity = Activity.Current;
                    var traceId = activity?.TraceId.ToString();
                    var spanId = activity?.SpanId.ToString();

                    var logMessage = new
                    {
                        Message = value,
                        TraceId = traceId,
                        SpanId = spanId,
                        Timestamp = DateTime.UtcNow
                    };

                    var kafkaMessage = new Message<string, string>
                    {
                        Key = key,
                        Value = JsonSerializer.Serialize(logMessage)
                    };

                    var result = await _producer.ProduceAsync(topic, kafkaMessage);
                    _logger.LogInformation(
                        "Produced message to {TopicPartitionOffset} (TraceId: {TraceId}, SpanId: {SpanId})",
                        result.TopicPartitionOffset,
                        traceId,
                        spanId);
                });
            });
        }

        private async Task EnsureTopicExists(string bootstrapServers, string topicName)
        {
            var config = new AdminClientConfig { BootstrapServers = bootstrapServers };
            using var adminClient = new AdminClientBuilder(config).Build();
            try
            {
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                if (!metadata.Topics.Any(t => t.Topic == topicName))
                {
                    await adminClient.CreateTopicsAsync(new[]
                    {
                        new TopicSpecification { Name = topicName, NumPartitions = 3, ReplicationFactor = 1 }
                    });
                    _logger.LogInformation("Topic '{TopicName}' created successfully.", topicName);
                }
            }
            catch (CreateTopicsException ex)
            {
                _logger.LogWarning("Topic creation failed: {Reason}", ex.Results[0].Error.Reason);
            }
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}