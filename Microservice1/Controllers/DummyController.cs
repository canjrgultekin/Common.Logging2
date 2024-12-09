using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using OpenTelemetry.Trace;
using System.Threading.Tasks;
using Common.Logging.Application.Interfaces;

namespace Microservice1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DummyController : ControllerBase
    {
        private readonly Tracer _tracer;
        private readonly IMessageRouter _messageRouter; // IKafkaProducerService yerine IMessageRouter

        public DummyController(TracerProvider tracerProvider, IMessageRouter messageRouter)
        {
            _tracer = tracerProvider.GetTracer("Microservice1");
            _messageRouter = messageRouter; // IKafkaProducerService yerine IMessageRouter
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
                await _messageRouter.RouteMessageAsync("logs-topic", "info", $"Dummy data retrieved successfully: {data}", CancellationToken.None);

                return Ok(data);
            }
            catch (Exception ex)
            {
                // Log exception to Kafka
                await _messageRouter.RouteMessageAsync("logs-topic", "error", ex.Message, CancellationToken.None);

                span.SetStatus(Status.Error.WithDescription(ex.Message));
                throw;
            }
        }
    }
}
