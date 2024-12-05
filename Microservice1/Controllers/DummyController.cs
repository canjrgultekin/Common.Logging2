using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using OpenTelemetry.Trace;
using Common.Logging;

namespace Microservice1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DummyController : ControllerBase
    {
        private readonly Tracer _tracer;
        private readonly KafkaProducerService _kafkaProducer;

        public DummyController(TracerProvider tracerProvider, KafkaProducerService kafkaProducer)
        {
            _tracer = tracerProvider.GetTracer("Microservice1");
            _kafkaProducer = kafkaProducer;
        }

        [HttpGet]
        [Route("getdata")]
        public async Task<IActionResult> GetData()
        {
            using var span = _tracer.StartActiveSpan("Microservice1.GetData");

            try
            {
                // Dummy data
                var data = new { Message = "Hello from Microservice1" };

                // Log to Kafka
                await _kafkaProducer.ProduceAsync("logs-topic", "info", $"Dummy data retrieved successfully: {data}");

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
