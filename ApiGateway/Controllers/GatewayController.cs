using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using System.Diagnostics;
using Common.Logging;
using System.Net.Http;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GatewayController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly Tracer _tracer;
        private readonly KafkaProducerService _kafkaProducer;
        private readonly IConfiguration _configuration;


        public GatewayController(HttpClient httpClient,TracerProvider tracerProvider, KafkaProducerService kafkaProducer,IConfiguration configuration)
        {
            _httpClient = httpClient;
            _tracer = tracerProvider.GetTracer("ApiGateway");
            _kafkaProducer = kafkaProducer;
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

                // Log to Kafka
                await _kafkaProducer.ProduceAsync("logs-topic", "info", $"Data retrieved successfully from Microservice1: {data}");

                return Ok(data);
            }
            catch (Exception ex)
            {
                // Log exception to Kafka
                await _kafkaProducer.ProduceAsync("logs-topic", "error", ex.Message);

                span.SetStatus(Status.Error.WithDescription(ex.Message));
                throw;
            }
        }
    }
}
