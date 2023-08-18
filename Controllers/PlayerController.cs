using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Microsoft.AspNetCore.Mvc;
using osu.Shared.Serialization;
using Sunrise.Helpers;
using Sunrise.Objects;
using Sunrise.Services;
using Sunrise.Enums;

namespace Sunrise.Controllers;

[Controller]
[Route("/")]
public class PlayerController : ControllerBase
{
    private readonly BanchoService _bancho;
    private readonly PlayerRepository _playerRepository;
    private readonly ILogger<PlayerController> _logger;

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
            catch (Exception e)
            {
                return BadRequest();
            }
            
            foreach (var packet in BanchoSerializer.DeserializePackets(ms))
            {
                #if DEBUG
                _logger.LogError(packet.Type.ToString());
                #endif
                
                //don't like this big switch:case so need to remake this somehow
                switch (packet.Type)
                {
                    case PacketType.ClientStatusRequestOwn:
                        _bancho.SendUserStats();
                        break;
                    case PacketType.ClientDisconnect:
                        _bancho.HandleQuit();
                        break;
                    case PacketType.ClientUserStatsRequest:
                        var msa = new MemoryStream(packet.Data);
                        var reader = new SerializationReader(msa);
                        
                        var ids = new List<int>();

                        int length = reader.ReadInt16();
                        for (var i = 0; i < length; i++) 
                            ids.Add(reader.ReadInt32());
                        
                        foreach (var value in ids.Where(value => _playerRepository.ContainsPlayer(value)))
                        {
                            _bancho.SendUserStats(_playerRepository.GetPlayer(value));
                        }
                        break;
                    case PacketType.ClientUserStatus:
                        var status = new BanchoUserStatus(packet.Data);
                        _bancho.UpdateUserStatus(status);
                        break;
                    case PacketType.ClientPong:
                        break;
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

        _bancho.SendAllChannels();
        
        var bytesToSend = _bancho.GetPacketBytes();

        Response.Headers.Add("cho-protocol", "19");
        Response.Headers.Add("cho-token", $"{player.Token}");
        Response.Headers.Add("Connection", "keep-alive");

        return new FileContentResult(bytesToSend, "application/octet-stream");
    }
}