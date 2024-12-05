using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Common.Logging
{
    public static class OpenTelemetryExtensions
    {
        public static void AddOpenTelemetryTracing(this IServiceCollection services, IConfiguration configuration)
        {
            var serviceName = configuration["OpenTelemetry:ServiceName"];
            var jaegerHost = configuration["Jaeger:AgentHost"];
            var jaegerPort = int.Parse(configuration["Jaeger:AgentPort"]);

            services.AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddJaegerExporter(options =>
                        {
                            options.AgentHost = jaegerHost;
                            options.AgentPort = jaegerPort;
                        });
                });
        }
    }
}
