namespace Common.Logging
{
    public interface IKafkaSettings
    {
        string BootstrapServers { get; }
        string Topic { get; }
        string GroupId { get; }
        KafkaConsumerSettings Consumer { get; }
        KafkaProducerSettings Producer { get; }

    }

}
