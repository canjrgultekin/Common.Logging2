using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Logging
{
    public interface IMessageRouter
    {
        Task RouteMessageAsync(string topic, string key, string value, CancellationToken cancellationToken);
    }

}
