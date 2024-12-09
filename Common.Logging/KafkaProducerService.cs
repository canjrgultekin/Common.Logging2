using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Polly;
using Nest;
using Policy = Polly.Policy;

namespace Common.Logging
{
    public class KafkaProducerService : IKafkaProducerService, IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaProducerService> _logger;
        private readonly AsyncPolicy _retryPolicy;
        private readonly AsyncPolicy _circuitBreakerPolicy;
        private readonly IElasticClient _elasticClient;
        private bool _disposed;
        private string bootstrapServers;
        public KafkaProducerService(
            IKafkaSettings _kafkaSettings,
            IElasticClient elasticClient,
            ILogger<KafkaProducerService> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
            bootstrapServers = _kafkaSettings.BootstrapServers;
            var config = new ProducerConfig
            {
                BootstrapServers = _kafkaSettings.BootstrapServers,
                SecurityProtocol = SecurityProtocol.Plaintext, // SASL düz metin
                MessageMaxBytes = 10485760, // 10MB
                SocketReceiveBufferBytes = 10485760, // 10MB
                SocketSendBufferBytes = 10485760,     // 10MB
                SslCaLocation = "",
                SslCertificateLocation = "",
                SslKeyLocation = "",
                RetryBackoffMs = _kafkaSettings.Producer.RetryBackoffMs,
                MessageTimeoutMs = _kafkaSettings.Producer.MessageTimeoutMs,
                AllowAutoCreateTopics = _kafkaSettings.Producer.AllowAutoCreateTopics
                
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
                .WaitAndRetryAsync(_kafkaSettings.Producer.RetryPolicy.MaxRetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(_kafkaSettings.Producer.RetryPolicy.BaseDelaySeconds, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} for KafkaProducer: {exception.Message}");
                    });

            // Circuit Breaker Policy
            _circuitBreakerPolicy = Policy
                .Handle<ProduceException<string, string>>()
                .CircuitBreakerAsync(_kafkaSettings.Producer.CircuitBreaker.FailureThreshold, TimeSpan.FromSeconds(_kafkaSettings.Producer.CircuitBreaker.DurationOfBreakSeconds),
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
        }

        public async Task ProcessMessageAsync(
            string mode, // "log" veya "event" seçimi
            string topicOrIndex, // Kafka topic veya Elasticsearch index
            string key,
            string value,
            CancellationToken cancellationToken)
        {
            if (mode == "log")
            {
                await LogToElasticSearchAsync(topicOrIndex, value, cancellationToken);
            }
            else if (mode == "event")
            {
                await ProduceToKafkaAsync(topicOrIndex, key, value, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Invalid mode specified: {Mode}", mode);
            }
        }

        private async Task ProduceToKafkaAsync(string topic, string key, string value, CancellationToken cancellationToken)
        {
            await EnsureTopicExistsAsync(bootstrapServers,topic);

            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    

                    var activity = Activity.Current;
                    var traceId = activity?.TraceId.ToString();
                    var spanId = activity?.SpanId.ToString();

                    var message = new
                    {
                        Message = value,
                        TraceId = traceId,
                        SpanId = spanId,
                        Timestamp = DateTime.UtcNow
                    };

                    var kafkaMessage = new Message<string, string>
                    {
                        Key = key,
                        Value = JsonSerializer.Serialize(message)
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

        private async Task LogToElasticSearchAsync(string index, string message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(index))
            {
                throw new ArgumentException("Index cannot be null or empty.", nameof(index));
            }

            var traceId = Activity.Current?.TraceId.ToString() ?? "UnknownTraceId";
            var spanId = Activity.Current?.SpanId.ToString() ?? "UnknownSpanId";

            var logDocument = new
            {
                Message = message,
                TraceId = traceId,
                SpanId = spanId,
                Timestamp = DateTime.UtcNow
            };

            var response = await _elasticClient.IndexAsync(logDocument, i => i.Index(index), cancellationToken);

            if (!response.IsValid)
            {
                var errorMessage = response.OriginalException?.Message ?? "Unknown error";
                _logger.LogError("Failed to log message to Elasticsearch: {Error}", errorMessage);
                throw new Exception($"Elasticsearch log failed: {errorMessage}", response.OriginalException);
            }

            _logger.LogInformation("Message successfully logged to Elasticsearch index: {Index}", index);
        }

        private async Task EnsureTopicExistsAsync(string bootstrapServers, string topicName)
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
            if (!_disposed)
            {
                _producer?.Dispose();
                _disposed = true;
            }
        }
    }
}
