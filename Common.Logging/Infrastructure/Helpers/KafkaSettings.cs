using Common.Logging.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Common.Logging
{
    public class KafkaSettings : IKafkaSettings
    {
        public string BootstrapServers { get; set; }
        public string Topic { get; set; }
        public string GroupId { get; set; }
        public KafkaConsumerSettings Consumer { get; set; }
        public KafkaProducerSettings Producer { get; set; }

        public static KafkaSettings Load(IConfiguration configuration)
        {
            return configuration.GetSection("Kafka").Get<KafkaSettings>();
        }
    }

    public class KafkaConsumerSettings
    {
        public string AutoOffsetReset { get; set; }
        public int SessionTimeoutMs { get; set; }
        public int MaxPollIntervalMs { get; set; }
        public KafkaRetryPolicySettings RetryPolicy { get; set; }
        public KafkaCircuitBreakerSettings CircuitBreaker { get; set; }
    }

    public class KafkaProducerSettings
    {
        public int RetryBackoffMs { get; set; }
        public int MessageTimeoutMs { get; set; }
        public bool AllowAutoCreateTopics { get; set; }
        public int TopicPartitions { get; set; }
        public short TopicReplicationFactor { get; set; }
        public KafkaRetryPolicySettings RetryPolicy { get; set; }
        public KafkaCircuitBreakerSettings CircuitBreaker { get; set; }
    }

    public class KafkaRetryPolicySettings
    {
        public int MaxRetryAttempts { get; set; }
        public int BaseDelaySeconds { get; set; }
    }

    public class KafkaCircuitBreakerSettings
    {
        public int FailureThreshold { get; set; }
        public int DurationOfBreakSeconds { get; set; }
    }

}
