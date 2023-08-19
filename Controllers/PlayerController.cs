using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Helpers;
using Sunrise.Objects;
using Sunrise.Services;
using Sunrise.Enums;
using Sunrise.Handlers;

namespace Sunrise.Controllers;

[Controller]
[Route("/")]
public class PlayerController : ControllerBase
{
    private readonly BanchoService _bancho;
    private readonly PlayerRepository _playerRepository;
    private readonly ILogger<PlayerController> _logger;
    private Dictionary<PacketType, IHandler> _hDictionary = HandlerDictionary.Handlers; 

    public PlayerController(BanchoService bancho, PlayerRepository player, ILogger<PlayerController> logger)
    {
        _bancho = bancho;
        _playerRepository = player;
        _logger = logger;
    }
    
    [HttpGet]
    public async Task<IActionResult> Get() 
    {
        return Ok("hello world");
    }

    [HttpPost]
    public async Task<IActionResult> Connect()
    {
        Player player;
        
        if (Request.Headers.TryGetValue("osu-token", out var token))
        {
            var ms = new MemoryStream();
            
            await Request.Body.CopyToAsync(ms);
            ms.Position = 0;
            
            try
            {
                player = _playerRepository.GetPlayer(token);
                _bancho.Player = player;
            }
            catch (Exception)
            {
                return BadRequest("Cannot find a player.");
            }
            
            foreach (var packet in BanchoSerializer.DeserializePackets(ms))
            {
                #if DEBUG
                    _logger.LogError(packet.Type.ToString());
                #endif

                if (_hDictionary.TryGetValue(packet.Type, out var handler))
                {
                    handler.Handle(packet, player, _bancho, _playerRepository);
                }
            }

            var bytes = _bancho.GetPacketBytes();

            return new FileContentResult(bytes, "application/octet-stream");
        }

        var sr = await new StreamReader(Request.Body).ReadToEndAsync();
        var loginRequest = LoginParser.Parse(sr);

        //only for testing
        player = new Player(new Random().Next(5000000, 11_000_000), loginRequest.username, 69, loginRequest.utcOffset, UserPrivileges.Peppy);
        _playerRepository.Add(player);
        

        _bancho.Player = player;
        _bancho.SendProtocolVersion();
        _bancho.SendLoginResponse(LoginResponses.Success);
        
        _bancho.SendPrivilege();
        _bancho.SendUserDataSingle(player.Id);
        _bancho.SendNotification("Welcome!");
        _bancho.SendUserData();
        _bancho.SendUserStats();
        
        foreach (var pr in _playerRepository.GetAllPlayers())
        {
            _bancho.SendUserData(pr);
            _bancho.Player = pr;
            _bancho.SendUserData(player);
        }

        _bancho.SendExistingChannels();
        
        var bytesToSend = _bancho.GetPacketBytes();

        Response.Headers.Add("cho-protocol", "19");
        Response.Headers.Add("cho-token", $"{player.Token}");
        Response.Headers.Add("Connection", "keep-alive");

        return new FileContentResult(bytesToSend, "application/octet-stream");
    }
}