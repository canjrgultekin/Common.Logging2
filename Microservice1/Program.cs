using Common.Logging;
using Serilog;



var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.json
var configuration = builder.Configuration;

// Add custom service registrations
builder.Services.AddElasticsearch(configuration);
builder.Services.AddOpenTelemetryTracing(configuration);
builder.Services.AddSerilogLogging(configuration, builder.Host);
builder.Services.AddKafkaServices(configuration);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HTTP client support
builder.Services.AddHttpClient();

var app = builder.Build();

// Middleware configuration
app.UseSerilogRequestLogging(); // Log requests
app.UseMiddleware<CorrelationIdMiddleware>(); // Add Correlation ID Middleware
app.UseMiddleware<NotFoundMiddleware>(); // Add NotFound Middleware

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();