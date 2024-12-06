using Elasticsearch.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nest;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Common.Logging
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddElasticsearch(this IServiceCollection services, IConfiguration configuration)
        {
            var url = configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
            var username = configuration["Elasticsearch:Username"];
            var password = configuration["Elasticsearch:Password"];

            var connectionPool = new SingleNodeConnectionPool(new Uri(url));
            var settings = new ConnectionSettings(connectionPool)
                .DefaultIndex("default-index");

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                settings.BasicAuthentication(username, password);
            }

            var client = new ElasticClient(settings);

            services.AddSingleton<IElasticClient>(client);

            return services;
        }

        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, IConfiguration configuration)
        {
            var otlpEndpoint = configuration["OpenTelemetry:OtlpExporterEndpoint"];
            services.AddOpenTelemetry().WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(configuration["OpenTelemetry:ServiceName"]))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint))
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = configuration["Jaeger:AgentHost"];
                        options.AgentPort = int.Parse(configuration["Jaeger:AgentPort"]);
                    });
            });

            return services;
        }

        public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration, IHostBuilder hostBuilder)
        {
            hostBuilder.UseSerilog((context, config) =>
            {
                config.ReadFrom.Configuration(configuration)
                      .Enrich.With<OpenTelemetryEnricher>();
            });

            return services;
        }

        public static IServiceCollection AddKafkaServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHostedService<KafkaConsumerService>();
            services.AddSingleton<KafkaProducerService>();

            return services;
        }
    }
}
