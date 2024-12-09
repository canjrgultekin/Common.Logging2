using Elasticsearch.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            var zipkinEndpoint = configuration["Zipkin:Endpoint"];

            services.AddOpenTelemetry().WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(configuration["OpenTelemetry:ServiceName"]))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint))
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = configuration["Jaeger:AgentHost"];
                        options.AgentPort = int.Parse(configuration["Jaeger:AgentPort"]);
                    })
                    .AddZipkinExporter(o => o.Endpoint = new Uri(zipkinEndpoint));
            });

            return services;
        }

        public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
        {
            // Serilog'u global olarak yapılandır
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.With<OpenTelemetryEnricher>() // OpenTelemetry bağlamını zenginleştir
                .WriteTo.Console()
                .WriteTo.Elasticsearch(new Serilog.Sinks.Elasticsearch.ElasticsearchSinkOptions(new Uri(configuration["Elasticsearch:Url"]))
                {
                    AutoRegisterTemplate = true,
                    IndexFormat = "logs-{0:yyyy.MM.dd}"
                })
                .CreateLogger();

            // Serilog'u Microsoft.Extensions.Logging ile entegre et
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders(); // Varsayılan log sağlayıcılarını kaldır
                loggingBuilder.AddSerilog(dispose: true); // Serilog'u ekle
            });

            return services;
        }


        public static IServiceCollection AddKafkaServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IKafkaSettings>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return KafkaSettings.Load(config);
            });


       
            services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
            services.AddSingleton<IElasticLoggerService, ElasticLoggerService>();
            services.AddSingleton<IMessageRouter, MessageRouter>();
            services.AddHostedService<KafkaConsumerService>();

            return services;
        }

        public static IServiceCollection AddKafkaConsumerServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IKafkaSettings>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return KafkaSettings.Load(config);
            });



            services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
            services.AddSingleton<IElasticLoggerService, ElasticLoggerService>();
            services.AddSingleton<IMessageRouter, MessageRouter>();
            services.AddSingleton<KafkaConsumerService>();

            return services;
        }
    }
}
