using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Database.Schemas;
using Sunrise.GameClient.Helpers;
using Sunrise.GameClient.Objects;
using Sunrise.GameClient.Types.Enums;
using Sunrise.Utils;

namespace Sunrise.GameClient.Services;

public class LoginService
{
    private readonly ServicesProvider _services;
    private readonly RegionHelper _regionHelper;

    public LoginService(ServicesProvider services)
    {
        _regionHelper = new RegionHelper();
        _services = services;
    }

    public async Task<IActionResult> Handle(HttpRequest request, HttpResponse response)
    {
        var sr = await new StreamReader(request.Body).ReadToEndAsync();
        var loginRequest = Parsers.ParseLogin(sr);
        var ip = _regionHelper.GetUserIpAddress(request);

        response.Headers.Add("cho-protocol", "19");
        response.Headers.Add("Connection", "keep-alive");


        var user = await _services.Database.GetUser(username: loginRequest.username);
        var location = await _regionHelper.GetRegion(ip);
        location.TimeOffset = loginRequest.utcOffset;

        if (user == null)
        {
            // Temporary solution, will be replaced with registration system
            user = new User().SetUserStats(loginRequest.username, loginRequest.passHash, _regionHelper.GetCountryCode(location.Country), PlayerRank.Supporter);
            user = await _services.Database.InsertUser(user); // Returns the user with the ID assigned
        }

        if (user.Passhash != loginRequest.passHash)
            return Reject(response, "Invalid credentials.");

        if (_services.Sessions.IsUserOnline(user.Id))
            return Reject(response, "User is already logged in.");


        var session = _services.Sessions.CreateSession(user, location);

        response.Headers.Add("cho-token", $"{session.Token}");

        return Proceed(session);
    }

    public IActionResult Reject(HttpResponse response, string? reason = null)
    {
        response.Headers.Add("cho-token", $"no-token");

        var writer = new PacketWriter();
        writer.WritePacket(PacketType.ServerLoginReply, LoginResponses.InvalidCredentials);

        if (reason != null)
            writer.WritePacket(PacketType.ServerNotification, reason);

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    private IActionResult Proceed(Session session)
    {
        session.SendLoginResponse(LoginResponses.Success);
        session.SendProtocolVersion();
        session.SendPrivilege();
        session.SendExistingChannels();

        session.SendFriendsList();
        _services.Sessions.SendCurrentPlayers(session);

        _services.Sessions.WriteToAllSessions(PacketType.ServerUserPresence, session.Attributes.GetPlayerPresence());
        _services.Sessions.WriteToAllSessions(PacketType.ServerUserData, session.Attributes.GetPlayerData());

        session.SendUserData();
        session.SendUserPresence();

        session.SendNotification("Welcome on Sunrise server!");

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }

}