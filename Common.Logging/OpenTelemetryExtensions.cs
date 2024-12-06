using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;

namespace Common.Logging
{
    public static class OpenTelemetryExtensions
    {
        public static void AddOpenTelemetryTracing(this IServiceCollection services, IConfiguration configuration)
        {
            var serviceName = configuration["OpenTelemetry:ServiceName"];
            var jaegerHost = configuration["Jaeger:AgentHost"];
            var jaegerPort = configuration.GetValue<int?>("Jaeger:AgentPort");

            if (string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(jaegerHost) || !jaegerPort.HasValue)
            {
                throw new ArgumentException("OpenTelemetry configuration is invalid. Please check your settings.");
            }

           services.AddOpenTelemetry()
               .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation() // EF Core için instrumentasyon
                    .AddRedisInstrumentation() // Redis için instrumentasyon
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = jaegerHost;
                        options.AgentPort = jaegerPort.Value;
                    });

                // Hata kontrol mekanizması
                builder.AddSource(serviceName);
                builder.Configure(options =>
                {
                    options.ResourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName);
                });
            });
        }
    }
}
