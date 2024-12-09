using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Logging.Application.Interfaces
{
    public interface IElasticLoggerService
    {
        Task LogAsync(string index, string message, CancellationToken cancellationToken);
    }
}
