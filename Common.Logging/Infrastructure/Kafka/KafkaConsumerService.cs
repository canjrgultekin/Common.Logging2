using Common.Logging.Application.Interfaces;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using System.Collections.Concurrent;
using Policy = Polly.Policy;

namespace Common.Logging.Infrastructure.Kafka
{
    public class KafkaConsumerService : BackgroundService, IDisposable
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly IMessageRouter _messageRouter;
        private readonly AsyncPolicy _retryPolicy;
        private readonly AsyncPolicy _circuitBreakerPolicy;
        private readonly string _topic;
        public KafkaConsumerService(
            IKafkaSettings _kafkaSettings,
            IMessageRouter messageRouter,
            ILogger<KafkaConsumerService> logger)
        {

            _logger = logger;
            _messageRouter = messageRouter;
            _topic = _kafkaSettings.Topic ?? "default-topic";

            var config = new ConsumerConfig
            {
                BootstrapServers = _kafkaSettings.BootstrapServers,
                GroupId = _kafkaSettings.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                SecurityProtocol = SecurityProtocol.Plaintext, // SASL düz metin
                MessageMaxBytes = 10485760, // 10MB
                SocketReceiveBufferBytes = 10485760, // 10MB
                SocketSendBufferBytes = 10485760,     // 10MB
                SslCaLocation = "",
                SslCertificateLocation = "",
                SslKeyLocation = "",
                SessionTimeoutMs = _kafkaSettings.Consumer.SessionTimeoutMs > 0 ? _kafkaSettings.Consumer.SessionTimeoutMs : 8000,
                MaxPollIntervalMs = _kafkaSettings.Consumer.MaxPollIntervalMs > 0 ? _kafkaSettings.Consumer.MaxPollIntervalMs : 300000

            };

            _consumer = new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, error) => _logger.LogError($"Kafka error: {error.Reason}"))
                .Build();

            // Retry Policy
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(_kafkaSettings.Consumer.RetryPolicy.MaxRetryAttempts, retryAttempt => TimeSpan.FromSeconds(Math.Pow(_kafkaSettings.Consumer.RetryPolicy.BaseDelaySeconds, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} for KafkaConsumer: {exception.Message}");
                    });

            // Circuit Breaker Policy
            _circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(_kafkaSettings.Consumer.CircuitBreaker.FailureThreshold, TimeSpan.FromSeconds(_kafkaSettings.Consumer.CircuitBreaker.DurationOfBreakSeconds),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError($"Circuit broken for KafkaConsumer: {exception.Message}. Duration: {duration.TotalSeconds} seconds.");
                    },
                    onReset: () => _logger.LogInformation("Circuit reset for KafkaConsumer."),
                    onHalfOpen: () => _logger.LogWarning("Circuit is half-open for KafkaConsumer."));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _consumer.Subscribe(_topic);
            _logger.LogInformation($"KafkaConsumerService started listening on topic: {_topic}");

            var tasks = new ConcurrentBag<Task>();

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await _retryPolicy.ExecuteAsync(async () =>
                    {
                        await _circuitBreakerPolicy.ExecuteAsync(async () =>
                        {
                            // Kafka'dan mesaj al
                            var result = _consumer.Consume(stoppingToken);
                            if (result != null)
                            {
                                _logger.LogInformation("Message received: {Value} on topic: {Topic}", result.Message.Value, result.Topic);

                                // Mesajı MessageRouter üzerinden yönlendir
                                tasks.Add(Task.Run(() =>
                                    _messageRouter.RouteMessageAsync(
                                        topic: result.Topic,
                                        key: result.Message.Key,
                                        value: result.Message.Value,
                                        cancellationToken: stoppingToken
                                    )));
                            }
                        });
                    });
                }

                // Tüm task'lerin tamamlanmasını bekle
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("KafkaConsumerService is stopping.");
            }
            //finally
            //{
            //    _consumer.Close();
            //}
        }
        public async Task<IEnumerable<(string Key, string Value)>> ConsumeAndProcessMessagesAsync(CancellationToken stoppingToken)
        {
            var results = new List<(string Key, string Value)>();

            while (!stoppingToken.IsCancellationRequested)
            {
                var result = _consumer.Consume(stoppingToken);
                if (result != null)
                {
                    _logger.LogInformation("Message received: {Value} on topic: {Topic}", result.Message.Value, result.Topic);

                    // Gelen mesajı işleme
                    if (result.Message.Key == "events")
                    {
                        _logger.LogInformation("Key 'events' matched.");

                        // Örneğin: İşlenen mesajı listeye ekleyelim
                        results.Add((result.Message.Key, result.Message.Value));
                    }
                }
            }

            return results; // İşlenmiş mesajları döndür
        }
        public override void Dispose()
        {
            base.Dispose();
            _consumer?.Dispose();
        }
    }
}
