using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

public class NotFoundMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<NotFoundMiddleware> _logger;

    public NotFoundMiddleware(RequestDelegate next, ILogger<NotFoundMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode == (int)HttpStatusCode.NotFound)
        {
            // Loglama
            _logger.LogWarning("404 Not Found - Path: {Path}, Method: {Method}, Query: {QueryString}",
                context.Request.Path,
                context.Request.Method,
                context.Request.QueryString);

            // Özelleştirilmiş Yanıt
            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Message = "The requested resource was not found.",
                Path = context.Request.Path,
                Timestamp = DateTime.UtcNow
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
