using Microsoft.AspNetCore.Mvc;
using Sunrise.Database;
using Sunrise.Types.Enums;
using Sunrise.Types.Objects;
using Sunrise.Utils;

namespace Sunrise.Services;

public class ConnectionService
{
    private readonly BanchoService _banchoSession;
    private readonly ServicesProvider _services;

    public ConnectionService(BanchoService banchoSession, PlayersPoolService playersPool, ServicesProvider services)
    {
        _banchoSession = banchoSession;
        _services = services;
    }

    public async Task<IActionResult> SendLoginResponse(HttpRequest Request, HttpResponse Response)
    {
        var sr = await new StreamReader(Request.Body).ReadToEndAsync();
        var loginRequest = Parsers.ParseLogin(sr);

        PlayerObject player = null;
        var user = await _services.Database.GetUser(username: loginRequest.username);

        // If player is not found, create a new one
        if (user == null)
        {
            var token = Guid.NewGuid().ToString();
            var schema = new UserSchema();
            user = schema.SetUserStats(loginRequest.username, loginRequest.passHash, token, 0, UserPrivileges.Supporter);

            await _services.Database.InsertUser(user);
        }

        // Init local player object
        player = new PlayerObject(user);

        if (player.Player.HashedPassword != loginRequest.passHash)
        {
            _banchoSession.SendLoginResponse(LoginResponses.InvalidCredentials);

            Response.Headers.Add("cho-protocol", "19");
            Response.Headers.Add("cho-token", $"{user.Token}");
            Response.Headers.Add("Connection", "keep-alive");

            Console.WriteLine("Password is incorrect.");

            return new FileContentResult(_banchoSession.GetPacketBytes(), "application/octet-stream");
        }

        if (_services.Players.ContainsPlayer(player.Player.Id))
        {
            _banchoSession.SendLoginResponse(LoginResponses.InvalidCredentials);

            Response.Headers.Add("cho-protocol", "19");
            Response.Headers.Add("cho-token", $"{user.Token}");
            Response.Headers.Add("Connection", "keep-alive");

            Console.WriteLine("Player is already logged in.");

            return new FileContentResult(_banchoSession.GetPacketBytes(), "application/octet-stream");
        }

        Console.WriteLine("Password is correct. Update token.");

        _banchoSession.SetPlayer(user);
        _services.Players.Add(player.Player);

        Response.Headers.Add("cho-protocol", "19");
        Response.Headers.Add("cho-token", $"{user.Token}");
        Response.Headers.Add("Connection", "keep-alive");

        return await GetSignInBytes(player);
    }

    private Task<IActionResult> GetSignInBytes(PlayerObject player)
    {
        _banchoSession.SendProtocolVersion();
        _banchoSession.SendLoginResponse(LoginResponses.Success);

        _banchoSession.SendPrivilege();
        _banchoSession.SendNotification("Welcome on Sunrise server!");

        _banchoSession.SendUserData();
        _banchoSession.SendUserStats();
        _banchoSession.SendFriendsList();
        //_banchoSession.SetBanchoMaintenance();

        // send data to all players include us so we show as logged in
        foreach (var pr in _services.Players.GetAllPlayers())
        {
            _banchoSession.SendUserData(pr); // get user data to current client
        }

        // NOTE: Doesn't work. Sends all data to itself; should send to other players
        var packet = _banchoSession.GetUserData();
        _banchoSession.EnqueuePacketForEveryone(packet); // send current client data to all players

        _banchoSession.SendExistingChannels();

        var bytesToSend = _banchoSession.GetPacketBytes();

        return Task.FromResult<IActionResult>(new FileContentResult(bytesToSend, "application/octet-stream"));
    }

}