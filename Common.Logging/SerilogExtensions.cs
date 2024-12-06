using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using System;

namespace Common.Logging
{
    public static class SerilogExtensions
    {
        public static void AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
        {
            var elasticUri = configuration["Elasticsearch:Url"];
            var minimumLogLevel = configuration.GetValue<string>("Serilog:MinimumLogLevel") ?? "Information";

            if (string.IsNullOrEmpty(elasticUri))
            {
                throw new ArgumentException("Elasticsearch URL is not configured. Please check your settings.");
            }

            var logLevel = minimumLogLevel switch
            {
                "Debug" => Serilog.Events.LogEventLevel.Debug,
                "Error" => Serilog.Events.LogEventLevel.Error,
                "Warning" => Serilog.Events.LogEventLevel.Warning,
                "Verbose" => Serilog.Events.LogEventLevel.Verbose,
                _ => Serilog.Events.LogEventLevel.Information
            };

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
                {
                    AutoRegisterTemplate = true,
                    IndexFormat = "logstash-{0:yyyy.MM.dd}",
                    ModifyConnectionSettings = connectionConfiguration =>
                        connectionConfiguration.BasicAuthentication(
                            configuration["Elasticsearch:Username"],
                            configuration["Elasticsearch:Password"]),
                    FailureCallback = e => Console.WriteLine($"Unable to submit event: {e.Message}"),
                    EmitEventFailure = EmitEventFailureHandling.Retry
                })
                .CreateLogger();

            services.AddLogging(loggingBuilder =>
                loggingBuilder.AddSerilog(dispose: true));
        }
    }
}
