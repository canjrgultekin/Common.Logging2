using Common.Logging;

namespace EventSubscriber
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly KafkaConsumerService _kafkaConsumer;
        private readonly IMessageRouter _messageRouter;
        public Worker(KafkaConsumerService kafkaConsumer, IMessageRouter messageRouter, ILogger<Worker> logger)
        {
            _kafkaConsumer = kafkaConsumer;
            _messageRouter = messageRouter;
            _logger = logger;
           
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker service started.");

            try
            {
                var processedMessages = await _kafkaConsumer.ConsumeAndProcessMessagesAsync(stoppingToken);

                foreach (var message in processedMessages)
                {
                    _logger.LogInformation("Processed message: Key = {Key}, Value = {Value}", message.Key, message.Value);
                   
                    // Ýlgili mesaja göre Kafka'ya yeni bir event yolla
                    if (message.Key == "events-can")
                    {
                        await _messageRouter.RouteMessageAsync(
                             topic: "logs-topic",
                             key: message.Key,
                             value: "This is a event triggered message",
                             cancellationToken: stoppingToken);

                        await _messageRouter.RouteMessageAsync(
                             topic: "events-topic",
                             key: message.Key,
                             value: "New event generated based on processing",
                             cancellationToken: stoppingToken);
                    }      
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker service stopping...");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
