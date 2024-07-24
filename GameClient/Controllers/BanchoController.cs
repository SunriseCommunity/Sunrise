using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Sunrise.GameClient;
using Sunrise.Services;
using Sunrise.Types.Enums;
using Sunrise.Types.Interfaces;

namespace Sunrise.Controllers;

[Controller]
[Route("/")]
public class BanchoController : ControllerBase
{
    private readonly ConnectionService _connectionService;
    private readonly ServicesProvider _services;
    private readonly BanchoService _banchoSession;

    private readonly ILogger<BanchoController> _logger;
    private readonly Dictionary<PacketType, IHandler> _hDictionary = HandlerDictionary.Handlers;

    public BanchoController(ServicesProvider services, BanchoService banchoSession, ILogger<BanchoController> logger)
    {
        Console.WriteLine("BanchoController created");
        _services = services;
        _logger = logger;
        _banchoSession = banchoSession;
        _connectionService = new ConnectionService(banchoSession, services.Players, services);
    }

    [HttpGet]
    public Task<IActionResult> Get()
    {
        return Task.FromResult<IActionResult>(Ok("Hello, world!"));
    }

    [HttpPost]
    public async Task<IActionResult> Connect()
    {
        string? sessionToken = Request.Headers["osu-token"];

        // If no session token, then we need to log in
        if (sessionToken == null)
        {
            Console.WriteLine("No session token");
            return await _connectionService.SendLoginResponse(Request, Response);
        }

        await using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        ms.Position = 0;

        // Try to get the player from the database
        var player = await _services.Database.GetUser(token: sessionToken);

        if (player == null)
        {
            _logger.LogWarning($"Player not found in the database with token: {sessionToken}");

            _banchoSession.SendLoginResponse(LoginResponses.InvalidCredentials);
            return new FileContentResult(_banchoSession.GetPacketBytes(), "application/octet-stream");
        }

        _banchoSession.SetPlayer(player);

        // Deserialize packets and handle them
        foreach (var packet in BanchoSerializer.DeserializePackets(ms))
        {
            Console.WriteLine($"Packet type: {packet.Type}");

            if (packet.Type != PacketType.ClientPong)
                _logger.LogInformation($"Time: {DateTime.Now} | (Code: {(int)packet.Type} | String: {packet.Type})");

            if (_hDictionary.TryGetValue(packet.Type, out var handler))
            {
                handler.Handle(packet, _banchoSession, _services);
            }
            else
            {
                _logger.LogWarning("Handler not found for packet type: " + packet.Type);
            }
        }

        return new FileContentResult(_banchoSession.GetPacketBytes(), "application/octet-stream");
    }
}