using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Logging
{
    public interface IKafkaConsumerService
    {
        event Func<string, string, string, Task> OnMessageReceived;
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
