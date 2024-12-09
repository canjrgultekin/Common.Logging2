using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Common.Logging.Infrastructure.Helpers
{
    public class OpenTelemetryEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var activity = Activity.Current;

            if (activity != null)
            {
                try
                {
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));

                    // Opsiyonel olarak ParentId ekleme
                    if (!string.IsNullOrEmpty(activity.ParentId))
                    {
                        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ParentId", activity.ParentId));
                    }

                    // Opsiyonel TraceState ekleme
                    if (!string.IsNullOrEmpty(activity.TraceStateString))
                    {
                        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TraceState", activity.TraceStateString));
                    }
                }
                catch (Exception ex)
                {
                    // Log enrichment sırasında hata yakalama
                    logEvent.AddPropertyIfAbsent(
                        propertyFactory.CreateProperty("EnrichmentError", $"Error during enrichment: {ex.Message}"));
                }
            }
            else
            {
                // Activity null ise fallback mekanizması
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty("TraceId", "NoTraceId"));
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty("SpanId", "NoSpanId"));
            }
        }
    }
}
