using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Common.Logging.Application.Middleware
{
    public class CorrelationIdMiddleware
    {
        private const string TraceIdHeaderName = "X-Trace-ID";
        private const string SpanIdHeaderName = "X-Span-ID";
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Eğer Activity yoksa yeni bir izleme başlat
            var activity = Activity.Current ?? new Activity("CorrelationIdMiddleware").Start();

            if (activity != null)
            {
                try
                {
                    // TraceId ve SpanId'yi Response Headers'a ekle
                    context.Response.OnStarting(() =>
                    {
                        try
                        {
                            context.Response.Headers[TraceIdHeaderName] = activity.TraceId.ToString();
                            context.Response.Headers[SpanIdHeaderName] = activity.SpanId.ToString();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Error while setting trace headers: {Message}", ex.Message);
                        }
                        return Task.CompletedTask;
                    });

                    _logger.LogDebug("TraceId: {TraceId}, SpanId: {SpanId} set in headers.",
                        activity.TraceId.ToString(),
                        activity.SpanId.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error in CorrelationIdMiddleware: {Message}", ex.Message);
                }
            }

            // Middleware'in devamını çağır
            await _next(context);
        }
    }
}
