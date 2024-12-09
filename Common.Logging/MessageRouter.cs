using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Logging
{
    public class MessageRouter : IMessageRouter
    {
        private readonly IKafkaProducerService _kafkaProducerService;
        private readonly ILogger<MessageRouter> _logger;

        public MessageRouter(
            IKafkaProducerService kafkaProducerService,
            ILogger<MessageRouter> logger)
        {
            _kafkaProducerService = kafkaProducerService;
            _logger = logger;
        }

        public async Task RouteMessageAsync(string topic, string key, string value, CancellationToken cancellationToken)
        {
            if (topic == "logs")
            {
                _logger.LogInformation("Routing message to Elasticsearch via KafkaProducerService.");
                await _kafkaProducerService.ProcessMessageAsync("log", topic, key, value, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Routing message to Kafka topic: {Topic}", topic);
                await _kafkaProducerService.ProcessMessageAsync("event", topic, key, value, cancellationToken);
            }
        }
    }
}
