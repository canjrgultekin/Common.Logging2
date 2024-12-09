using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using Common.Logging;


namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GatewayController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly Tracer _tracer;
        private readonly IMessageRouter _messageRouter; // IKafkaProducerService yerine IMessageRouter
        private readonly IConfiguration _configuration;

        public GatewayController(HttpClient httpClient, TracerProvider tracerProvider, IMessageRouter messageRouter, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _tracer = tracerProvider.GetTracer("ApiGateway");
            _messageRouter = messageRouter; // IKafkaProducerService yerine IMessageRouter
            _configuration = configuration;
        }

        [HttpGet]
        [Route("getdata")]
        public async Task<IActionResult> GetData()
        {
            using var span = _tracer.StartActiveSpan("ApiGateway.GetData");

            try
            {
                var microservice1BaseUrl = _configuration["Microservice1:BaseUrl"];
                var response = await _httpClient.GetAsync($"{microservice1BaseUrl}/api/dummy/getdata");
                response.EnsureSuccessStatusCode();

                span.SetStatus(OpenTelemetry.Trace.Status.Ok);

                var data = await response.Content.ReadAsStringAsync();

                // Kafka'ya log kaydını gönder
                await _messageRouter.RouteMessageAsync("logs-topic", "info", $"Data retrieved successfully from Microservice1: {data}", CancellationToken.None);

                return Ok(data);
            }
            catch (Exception ex)
            {
                // Hata durumunda Kafka'ya log kaydını gönder
                await _messageRouter.RouteMessageAsync("logs-topic", "error", ex.Message, CancellationToken.None);

                span.SetStatus(Status.Error.WithDescription(ex.Message));
                throw;
            }
        }
    }
}
