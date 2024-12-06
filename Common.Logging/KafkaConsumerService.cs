using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nest;
using Polly;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Logging
{
    public class KafkaConsumerService : IHostedService, IDisposable
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly IElasticClient _elasticClient;
        private readonly AsyncPolicy _retryPolicy;
        private readonly AsyncPolicy _circuitBreakerPolicy;
        private readonly SemaphoreSlim _semaphore;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly string _topic;

        public KafkaConsumerService(
            IConfiguration configuration,
            ILogger<KafkaConsumerService> logger,
            IElasticClient elasticClient)
        {
            _logger = logger;
            _elasticClient = elasticClient;

            // Kafka yapılandırmasını oku
            var kafkaConfig = configuration.GetSection("Kafka").Get<IConfigurationSection>();
            var consumerConfig = kafkaConfig.GetSection("Consumer");
            // RetryPolicy yapılandırması
            var maxRetryAttempts = int.Parse(consumerConfig["RetryPolicy:MaxRetryAttempts"]);
            var baseDelaySeconds = int.Parse(consumerConfig["RetryPolicy:BaseDelaySeconds"]);

            var bootstrapServers = kafkaConfig["BootstrapServers"];
            var groupId = kafkaConfig["GroupId"];
            var sslCaLocation = kafkaConfig["SslCaLocation"];
            var sslCertificateLocation = kafkaConfig["SslCertificateLocation"];
            var sslKeyLocation = kafkaConfig["SslKeyLocation"];
            var autoOffsetReset = consumerConfig["AutoOffsetReset"];
            var sessionTimeoutMs = int.Parse(consumerConfig["SessionTimeoutMs"]);
            var maxPollIntervalMs = int.Parse(consumerConfig["MaxPollIntervalMs"]);

            _topic = kafkaConfig["Topic"] ?? "default-topic";

            // CircuitBreaker yapılandırması
            var failureThreshold = int.Parse(consumerConfig["CircuitBreaker:FailureThreshold"]);
            var durationOfBreakSeconds = int.Parse(consumerConfig["CircuitBreaker:DurationOfBreakSeconds"]);


            var config = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                AutoOffsetReset = autoOffsetReset == "Earliest" ? AutoOffsetReset.Earliest : AutoOffsetReset.Latest,
                SecurityProtocol = string.IsNullOrEmpty(sslCaLocation) ? SecurityProtocol.Plaintext : SecurityProtocol.Ssl,
                SslCaLocation = sslCaLocation,
                SslCertificateLocation = sslCertificateLocation,
                SslKeyLocation = sslKeyLocation,
                SessionTimeoutMs = sessionTimeoutMs,
                MaxPollIntervalMs = maxPollIntervalMs
            };
            
            _consumer = new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, error) =>
                {
                    _logger.LogError($"Kafka error: {error.Reason}");
                })
                .Build();

            _retryPolicy = Polly.Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts, retryAttempt => TimeSpan.FromSeconds(Math.Pow(baseDelaySeconds, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} for KafkaConsumer: {exception.Message}");
                    });

            _circuitBreakerPolicy = Polly.Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(failureThreshold, TimeSpan.FromSeconds(durationOfBreakSeconds),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError($"Circuit broken for KafkaConsumer: {exception.Message}. Duration: {duration.TotalSeconds} seconds.");
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit reset for KafkaConsumer.");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogWarning("Circuit is half-open for KafkaConsumer.");
                    });

            var semaphoreLimit = int.Parse(consumerConfig["SemaphoreLimit"]);
            _semaphore = new SemaphoreSlim(semaphoreLimit);

            EnsureTopicExists(config.BootstrapServers, _topic).Wait();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task.Run(() => StartConsuming(_cancellationTokenSource.Token), cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            _consumer.Close();
            return Task.CompletedTask;
        }

        private async Task StartConsuming(CancellationToken cancellationToken)
        {
            _consumer.Subscribe(_topic);

            while (!cancellationToken.IsCancellationRequested)
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await _circuitBreakerPolicy.ExecuteAsync(async () =>
                    {
                        var result = _consumer.Consume(cancellationToken);
                        await ProcessMessage(result.Message.Value);
                    });
                });
            }
        }

        private async Task ProcessMessage(string message)
        {
            await _semaphore.WaitAsync(); // Memory-safe kontrol
            try
            {
                var log = new { Message = message, Timestamp = DateTime.UtcNow };
                _logger.LogInformation("Processing message: {Message}", message);
                await _elasticClient.IndexDocumentAsync(log);
            }
            finally
            {
                _semaphore.Release();
            }
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
            _cancellationTokenSource?.Dispose();
            _consumer?.Dispose();
            _semaphore?.Dispose();
        }
    }
}
