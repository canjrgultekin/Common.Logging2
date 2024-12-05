using Common.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System;


var builder = WebApplication.CreateBuilder(args);

// Add Serilog configuration
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.With<OpenTelemetryEnricher>();
});


// Load configuration from appsettings.json
var configuration = builder.Configuration;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Serilog Configuration
//builder.Services.AddSerilogLogging(configuration);


// OpenTelemetry configuration
builder.Services.AddOpenTelemetry().WithTracing(builder =>
{
    builder
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(configuration["OpenTelemetry:ServiceName"]))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://otel-collector:4317"))
     .AddJaegerExporter(options =>
     {
         options.AgentHost = configuration["Jaeger:AgentHost"];
         options.AgentPort = int.Parse(configuration["Jaeger:AgentPort"]);
     });
});

builder.Services.AddHttpClient();
//builder.Services.AddOpenTelemetryTracing(builder.Configuration);

// Register KafkaConsumer as a hosted service
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddSingleton<KafkaProducerService>();



var app = builder.Build();

app.UseSerilogRequestLogging(); // Log requests
app.UseMiddleware<CorrelationIdMiddleware>(); // Add Correlation ID Middleware
app.UseMiddleware<NotFoundMiddleware>(); // Add NotFound Middleware

// Middleware configuration
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();