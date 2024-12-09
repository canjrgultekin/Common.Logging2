using Common.Logging.Application.Middleware;
using Common.Logging.Extentions;
using EventSubscriber;
using Microsoft.AspNetCore.Builder;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for logging
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

// Load configuration from appsettings.json
var configuration = builder.Configuration;

// Add custom service registrations
// Ensure these extension methods are implemented correctly
builder.Services.AddElasticsearch(configuration); // Add Elasticsearch
builder.Services.AddOpenTelemetryTracing(configuration); // Add OpenTelemetry tracing
builder.Services.AddSerilogLogging(configuration); // Add Serilog logging
builder.Services.AddKafkaConsumerServices(configuration); // Add Kafka services

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HTTP client support
builder.Services.AddHttpClient();

// Register Worker Service
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// Middleware configuration
app.UseSerilogRequestLogging(); // Add Serilog middleware
app.UseMiddleware<CorrelationIdMiddleware>(); // Add Correlation ID Middleware
app.UseMiddleware<NotFoundMiddleware>(); // Add NotFound Middleware

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
