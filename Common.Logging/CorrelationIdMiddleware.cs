using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
namespace Common.Logging
{
   

    public class CorrelationIdMiddleware
    {
        private const string TraceIdHeaderName = "X-Trace-ID";
        private const string SpanIdHeaderName = "X-Span-ID";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[TraceIdHeaderName] = activity.TraceId.ToString();
                    context.Response.Headers[SpanIdHeaderName] = activity.SpanId.ToString();
                    return Task.CompletedTask;
                });
            }

            await _next(context);
        }
    }

}

