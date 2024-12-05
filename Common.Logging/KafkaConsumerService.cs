using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nest;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Logging
{
    public class KafkaConsumerService : IHostedService
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly IConfiguration _configuration;
        private IElasticClient _elasticClient;
        private CancellationTokenSource _cancellationTokenSource;

        public KafkaConsumerService(IConfiguration configuration, ILogger<KafkaConsumerService> logger)
        {
            _logger = logger;
            _configuration = configuration;

            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"],
                GroupId = configuration["Kafka:GroupId"],
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();

            var settings = new ConnectionSettings(new Uri(configuration["Elasticsearch:Url"]))
                .DefaultIndex("logstash");

            _elasticClient = new ElasticClient(settings);
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

        private void StartConsuming(CancellationToken cancellationToken)
        {
            _consumer.Subscribe(_configuration["Kafka:Topic"]);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = _consumer.Consume(cancellationToken);
                    _logger.LogInformation($"Consumed message '{result.Message.Value}' at: '{result.TopicPartitionOffset}'.");

                    // Logları ElasticSearch'e gönderin
                    var log = new { Message = result.Message.Value, Timestamp = DateTime.UtcNow };
                    _elasticClient.IndexDocument(log);
                }
            }
            catch (OperationCanceledException)
            {
                _consumer.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error occurred: {ex.Message}");
            }
        }
    }
}
