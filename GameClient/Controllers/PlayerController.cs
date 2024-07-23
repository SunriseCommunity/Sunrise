using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Database.Sqlite;
using Sunrise.Services;
using Sunrise.Enums;
using Sunrise.Handlers;
using Sunrise.Types.Classes;
using Sunrise.Types.Objects;
using Sunrise.Utils;

namespace Sunrise.Controllers;

[Controller]
[Route("/")]
public class PlayerController : ControllerBase
{
    private readonly BanchoService _banchoSession;
    private readonly PlayerRepository _playerRepository;
    private readonly SqliteDatabase _database;
    private readonly ILogger<PlayerController> _logger;
    private readonly Dictionary<PacketType, IHandler> _hDictionary = HandlerDictionary.Handlers;

    public PlayerController(BanchoService bancho, PlayerRepository player, SqliteDatabase database, ILogger<PlayerController> logger)
    {
        _banchoSession = bancho;
        _database = database;
        _playerRepository = player;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok("What are you looking for? :)");
    }

    [HttpPost]
    public async Task<IActionResult> Connect()
    {
        string? sessionToken = Request.Headers["osu-token"];

        // If no session token, then we need to login
        if (sessionToken == null)
        {
            return HandleLogin(Request).Result;
        }

        // We should have osu-token after login
        var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        ms.Position = 0;

        try
        {
            // Validate token to user credentials
            Console.WriteLine("Tried to find a player.");

            Console.WriteLine("Change playeron line 59");

            _banchoSession.SetPlayer(_database.Players.GetPlayer(sessionToken, null));
            //  _playerRepository.Add(_banchoSession.Player);

        }
        catch (Exception e)
        {
            Console.WriteLine("Cannot find a player.");

            // show exception in console
            Console.WriteLine(e);

            // Force logout
            return BadRequest("Invalid token. Please login again.");
        }

        foreach (var packet in BanchoSerializer.DeserializePackets(ms))
        {
#if DEBUG
            if (packet.Type != PacketType.ClientPong)
                _logger.LogWarning("Time: " + DateTime.Now.TimeOfDay + $" (Code: {(int)packet.Type} | String: {packet.Type})");
#endif

            if (_hDictionary.TryGetValue(packet.Type, out var handler))
            {
                handler.Handle(packet, _banchoSession, _database);
            }
        }

        var bytes = _banchoSession.GetPacketBytes();

        return new FileContentResult(bytes, "application/octet-stream");

    }

    private async Task<IActionResult> HandleLogin(HttpRequest request)
    {
        PlayerObject? player;

        var sr = await new StreamReader(Request.Body).ReadToEndAsync();
        var loginRequest = Parsers.ParseLogin(sr);

        player = _database.Players.GetPlayer(null, loginRequest.username);

        // Register new player
        if (player == null)
        {
            Console.WriteLine("Registering a new player.");
            // TODO: Ignore id

            player = new PlayerObject(player: new Player(loginRequest.username, loginRequest.passHash, 0, 0, UserPrivileges.Normal));

            _database.Players.CreatePlayer(player);
        }

        if (player.GetPlayer().HashedPassword != loginRequest.passHash)
        {
            Console.WriteLine("Wrong password.");

            return BadRequest("Invalid credentials.");
        }
        else
        {
            Console.WriteLine("Password is correct. Update token.");


            player.GetPlayer().Token = Guid.NewGuid().ToString();
            _database.Players.UpdateToken(player.GetPlayer().Username, player.GetPlayer().Token);
            // _playerRepository.Add(player);
        }

        Console.WriteLine("Change player on line 129");
        _banchoSession.SetPlayer(player);
        _banchoSession.SendProtocolVersion();
        _banchoSession.SendLoginResponse(LoginResponses.Success);

        _banchoSession.SendPrivilege();
        _banchoSession.SendUserDataSingle(player.GetPlayer().Id);
        _banchoSession.SendNotification("Welcome on Sunrise server!");


        // Why? Arent we do it already after?
        _banchoSession.SendUserData();
        _banchoSession.SendUserStats();

        // send data to all players include us so we show as logged in
        // foreach (var pr in _playerRepository.GetAllPlayers())
        // {
        //     _banchoSession.SendUserData(pr); // get user data to current client
        //
        //     Console.WriteLine("Change player on line 150");
        //     _banchoSession.Player = pr; // mock player to send data to all players
        //     _banchoSession.SendUserData(player.GetPlayer()); // send current client data to all players
        // }

        // Console.WriteLine("Change player on line 155");
        // _banchoSession.Player = player;
        // _banchoSession.SendUserData(player);
        _banchoSession.SendExistingChannels();

        var bytesToSend = _banchoSession.GetPacketBytes();

        Response.Headers.Add("cho-protocol", "19");
        Response.Headers.Add("cho-token", $"{player.GetPlayer().Token}");
        Response.Headers.Add("Connection", "keep-alive");

        return new FileContentResult(bytesToSend, "application/octet-stream");
    }


}