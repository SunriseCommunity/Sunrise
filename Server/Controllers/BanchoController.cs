using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[ApiController]
[SubdomainAttribute("c", "c4", "cho")]
public class BanchoController(ServicesProvider services, ILogger<BanchoController> logger) : ControllerBase
{
    private readonly Dictionary<PacketType, IHandler> _hDictionary = HandlersDictionary.Handlers;
    private readonly List<PacketType> _hSuppressed = HandlersDictionary.Suppressed;

    [HttpPost("/")]
    public async Task<IActionResult> Connect()
    {
        string? sessionToken = Request.Headers["osu-token"];

        if (sessionToken == null)
        {
            return await new LoginService(services).Handle(Request, Response);
        }

        var session = services.Sessions.GetSession(sessionToken);

        if (session == null)
        {
            return new LoginService(services).Reject(Response);
        }

        session.Attributes.LastPingRequest = DateTime.UtcNow;

        await using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        ms.Position = 0;

        foreach (var packet in BanchoSerializer.DeserializePackets(ms))
        {
            if (!_hSuppressed.Contains(packet.Type))
            {
                logger.LogInformation($"Time: {DateTime.Now} | (Code: {(int)packet.Type} | String: {packet.Type})");
            }

            if (_hDictionary.TryGetValue(packet.Type, out var handler))
            {
                await handler.Handle(packet, session, services);
            }
            else
            {
                logger.LogWarning($"Handler not found for packet type: {packet.Type}");
            }
        }

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }

    [HttpGet("/")]
    public Task<IActionResult> Get()
    {
        return Task.FromResult<IActionResult>(Ok("Hello, world!"));
    }
}