using Nest;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Common.Logging
{
    public class ElasticLoggerService : IElasticLoggerService
    {
        private readonly IElasticClient _elasticClient;
        private readonly ILogger<ElasticLoggerService> _logger;

        public ElasticLoggerService(IElasticClient elasticClient, ILogger<ElasticLoggerService> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task LogAsync(string index, string message, CancellationToken cancellationToken)
        {
            var logDocument = new
            {
                Message = message,
                TraceId = Activity.Current?.TraceId.ToString(),
                SpanId = Activity.Current?.SpanId.ToString(),
                Timestamp = DateTime.UtcNow
            };

            var response = await _elasticClient.IndexAsync(logDocument, i => i
                .Index(index)
                .Refresh(Elasticsearch.Net.Refresh.True), cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogError("Failed to log message to Elasticsearch: {Error}", response.OriginalException?.Message);
                throw response.OriginalException;
            }

            _logger.LogInformation("Message successfully logged to Elasticsearch index: {Index}", index);
        }
    }

}
