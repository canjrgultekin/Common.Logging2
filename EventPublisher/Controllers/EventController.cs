using Common.Logging;
using Microsoft.AspNetCore.Mvc;

namespace EventPublisher.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventController : ControllerBase
    {
        private readonly IMessageRouter _messageRouter;

        public EventController(IMessageRouter messageRouter)
        {
            _messageRouter = messageRouter;
        }

        [HttpPost]
        [Route("publish")]
        public async Task<IActionResult> Publish([FromBody] string message)
        {
            await _messageRouter.RouteMessageAsync(
                     topic: "events-topic",
                     key: "events-can",
                     value: "This is a publish event message",
                     cancellationToken: CancellationToken.None);
          
            return Ok("Message published to Kafka.");
        }
    }
}
