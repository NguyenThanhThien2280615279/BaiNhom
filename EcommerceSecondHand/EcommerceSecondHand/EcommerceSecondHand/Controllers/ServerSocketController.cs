using EcommerceSecondHand.Infrastructure;
using Microsoft.AspNetCore.Mvc;

[Route("server-socket")]
public class ServerSocketController : Controller
{
    private readonly ConnectionRegistry _registry;
    private readonly IServerMessageBus _bus;

    public ServerSocketController(ConnectionRegistry registry, IServerMessageBus bus)
    {
        _registry = registry;
        _bus = bus;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.ClientCount = _registry.Count;
        ViewBag.ClientIds = _registry.ConnectionIds; // IReadOnlyCollection<string>
        return View();
    }

    [ValidateAntiForgeryToken]
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromForm] string message, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            await _bus.BroadcastAsync($"SERVER: {message}", ct);
            return Ok(new { sent = true });
        }
        return BadRequest(new { sent = false, error = "Empty message" });
    }


    // Server-Sent Events stream cho phần "Luồng log"
    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        await foreach (var line in _bus.SubscribeAsync(ct))
        {
            await Response.WriteAsync($"data: {line}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
