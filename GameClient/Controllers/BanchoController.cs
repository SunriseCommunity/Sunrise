using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Sunrise.GameClient.Helpers;
using Sunrise.GameClient.Objects;
using Sunrise.GameClient.Services;
using Sunrise.GameClient.Types.Interfaces;

namespace Sunrise.GameClient.Controllers;

[ApiController]
[Route("/")]
public class BanchoController : ControllerBase
{
    private readonly Dictionary<PacketType, IHandler> _hDictionary = HandlersDictionaryHelper.Handlers;
    private readonly List<PacketType> _hSuppressed = HandlersDictionaryHelper.Suppressed;
    private readonly ILogger<BanchoController> _logger;
    private readonly ServicesProvider _services;

    public BanchoController(ServicesProvider services, ILogger<BanchoController> logger)
    {
        _services = services;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Connect()
    {
        string? sessionToken = Request.Headers["osu-token"];

        if (sessionToken == null)
            return await new LoginService(_services).Handle(Request, Response);

        Session? session = _services.Sessions.GetSession(sessionToken);

        if (session == null)
            return new LoginService(_services).Reject(Response);

        // Deserialize packets and handle them
        await using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        ms.Position = 0;

        foreach (var packet in BanchoSerializer.DeserializePackets(ms))
        {

            if (!_hSuppressed.Contains(packet.Type))
                _logger.LogInformation($"Time: {DateTime.Now} | (Code: {(int)packet.Type} | String: {packet.Type})");

            if (_hDictionary.TryGetValue(packet.Type, out var handler))
            {
                handler.Handle(packet, session, _services);
            }
            else
            {
                if (!_hSuppressed.Contains(packet.Type))
                    _logger.LogWarning($"Handler not found for packet type: {packet.Type}");
            }
        }

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }

    [HttpGet]
    public Task<IActionResult> Get()
    {
        return Task.FromResult<IActionResult>(Ok("Hello, world!"));
    }
}