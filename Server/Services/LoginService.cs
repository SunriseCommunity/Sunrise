using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public class LoginService(ServicesProvider services)
{
    private readonly RegionHelper _regionHelper = new();

    public async Task<IActionResult> Handle(HttpRequest request, HttpResponse response)
    {
        var sr = await new StreamReader(request.Body).ReadToEndAsync();
        var loginRequest = Parsers.ParseLogin(sr);
        var ip = _regionHelper.GetUserIpAddress(request);

        response.Headers["cho-protocol"] = "19";
        response.Headers.Connection = "keep-alive";

        var user = await services.Database.GetUser(username: loginRequest.username);
        var location = await _regionHelper.GetRegion(ip);
        location.TimeOffset = loginRequest.utcOffset;

        if (!CharactersFilter.IsValidString(loginRequest.username, true))
        {
            return Reject(response, "Invalid characters in username or password.");
        }

        if (user == null)
        {
            // Temporary solution, will be replaced with registration system
            user = new User
            {
                Username = loginRequest.username,
                Passhash = loginRequest.passHash,
                Country = _regionHelper.GetCountryCode(location.Country),
                Privilege = PlayerRank.Supporter
            };

            user = await services.Database.InsertUser(user); // Returns the user with the ID assigned
        }

        if (user.Passhash != loginRequest.passHash)
        {
            return Reject(response, "Invalid credentials.");
        }

        if (services.Sessions.IsUserOnline(user.Id))
        {
            return Reject(response, "User is already logged in.");
        }

        var session = services.Sessions.CreateSession(user, location);

        response.Headers["cho-token"] = $"{session.Token}";

        return await Proceed(session);
    }

    public IActionResult Reject(HttpResponse response, string? reason = null)
    {
        response.Headers["cho-token"] = "no-token";

        var writer = new PacketHelper();

        if (reason != null)
        {
            writer.WritePacket(PacketType.ServerNotification, reason);
        }

        writer.WritePacket(PacketType.ServerLoginReply, LoginResponses.InvalidCredentials);

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    public IActionResult Relogin(HttpResponse response, string? reason = null)
    {
        var writer = new PacketHelper();
        writer.WritePacket(PacketType.ServerRestart, 0); // Forces the client to relogin

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    private async Task<IActionResult> Proceed(Session session)
    {
        session.SendLoginResponse(LoginResponses.Success);
        session.SendProtocolVersion();
        session.SendPrivilege();
        session.SendExistingChannels();

        session.SendFriendsList();
        await services.Sessions.SendCurrentPlayers(session);

        services.Sessions.WriteToAllSessions(PacketType.ServerUserPresence, await session.Attributes.GetPlayerPresence());
        services.Sessions.WriteToAllSessions(PacketType.ServerUserData, await session.Attributes.GetPlayerData());

        await session.SendUserData();
        await session.SendUserPresence();

        session.SendNotification(Configuration.WelcomeMessage);

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }
}