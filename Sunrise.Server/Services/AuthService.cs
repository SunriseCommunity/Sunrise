using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Sunrise.Server.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Services;

public class AuthService(DatabaseService database, SessionRepository sessions, UserAuthService userAuthService, UserBanchoService userBanchoService, ChatChannelRepository chatChannelRepository)
{
    [TraceExecution]
    public async Task<FileContentResult> Login(HttpRequest request, HttpResponse response)
    {
        var sr = await new StreamReader(request.Body).ReadToEndAsync();
        var loginRequest = ServerParsers.ParseLogin(sr);
        var ip = RegionService.GetUserIpAddress(request);

        response.Headers["cho-protocol"] = "19";
        response.Headers.Connection = "keep-alive";

        var (user, getUserError) = await userBanchoService.GetUserFromLoginRequest(loginRequest, ip);

        if (getUserError != null || user == null)
        {
            var (error, loginResponseCode) = getUserError ?? ("Error retrieving user", LoginResponse.InvalidCredentials);
            return RejectLogin(response, error, loginResponseCode);
        }

        var (session, getUserSessionError) = await userBanchoService.GetNewUserSession(user, loginRequest, ip);

        if (getUserSessionError != null || session == null)
        {
            var (error, loginResponseCode) = getUserSessionError ?? ("Error creating user session", LoginResponse.InvalidCredentials);
            return RejectLogin(response, error, loginResponseCode);
        }

        var addEventResult = await database.Events.Users.AddUserLoginEvent(new UserEventAction(user, ip.ToString(), session.UserId), true, sr);
        if (addEventResult.IsFailure)
            return RejectLogin(response, addEventResult.Error);

        response.Headers["cho-token"] = session.Token;

        if (request.Headers["X-Using-Old-Domain"] == "true")
            return RejectLogin(response, $"You are using old domain name, please try to connect with:\n \"-devserver {Configuration.Domain}\".\nIf you have any problems with connection, contact staff at discord.");

        return await ProceedWithLogin(session, user);
    }

    public FileContentResult Relogin()
    {
        var writer = new PacketHelper();
        writer.WritePacket(PacketType.ServerRestart, 0); // Forces the client to relogin

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    private static FileContentResult RejectLogin(HttpResponse response, string? reason = null,
        LoginResponse code = LoginResponse.InvalidCredentials)
    {
        response.Headers["cho-token"] = "no-token";

        var writer = new PacketHelper();

        if (reason != null)
            writer.WritePacket(PacketType.ServerNotification, reason);

        writer.WritePacket(PacketType.ServerLoginReply, code);

        Log.Warning("Login rejected: {Reason}", reason ?? "No reason provided");

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    [TraceExecution]
    private async Task<FileContentResult> ProceedWithLogin(Session session, User sessionUser)
    {
        session.SendLoginResponse(LoginResponse.Success);
        session.SendProtocolVersion();
        session.SendPrivilege();
        session.SendExistingChannels();

        chatChannelRepository.JoinChannel("#osu", session);
        chatChannelRepository.JoinChannel("#announce", session);

        if (sessionUser.Privilege.HasFlag(UserPrivilege.Admin)) chatChannelRepository.JoinChannel("#staff", session);

        foreach (var channel in chatChannelRepository.GetChannels(session))
        {
            session.SendChannelAvailable(channel);
        }

        var friends = sessionUser.UserInitiatedRelationships.Where(r => r.Relation == UserRelation.Friend).Select(r => r.TargetId).ToList();
        session.SendFriendsList(friends);

        var sendCurrentPlayersResult = await sessions.SendCurrentPlayers(session);
        if (sendCurrentPlayersResult.IsFailure)
            Log.Error("Error sending current players to user with id of {SessionUserId} (It's not critical, so proceeding with login): {Error}",
                sessionUser.Id,
                sendCurrentPlayersResult.Error);

        if (sessionUser.SilencedUntil > DateTime.UtcNow)
            session.SendSilenceStatus((int)(sessionUser.SilencedUntil - DateTime.UtcNow).TotalSeconds);

        sessions.WriteToAllSessions(PacketType.ServerUserPresence, await session.Attributes.GetPlayerPresence(sessionUser));
        sessions.WriteToAllSessions(PacketType.ServerUserData, await session.Attributes.GetPlayerData(sessionUser));

        await session.SendUserData(sessionUser);
        await session.SendUserPresence(sessionUser);

        session.SendNotification(Configuration.WelcomeMessage);

        if (Configuration.OnMaintenance)
            session.SendNotification(
                "Server is currently in maintenance mode. Please keep in mind that some features may not work properly.");

        return new FileContentResult(session.GetContent(), "application/octet-stream");
    }

    public async Task<IActionResult> Register(HttpRequest request)
    {
        var username = (string)request.Form["user[username]"]!;
        var password = (string)request.Form["user[password]"]!;
        var email = (string)request.Form["user[user_email]"]!;

        var ip = RegionService.GetUserIpAddress(request);

        if (string.IsNullOrEmpty(ip.ToString()))
            return new BadRequestObjectResult("Invalid request: Missing IP address");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            return new BadRequestObjectResult("Invalid request: Missing parameters");

        if (request.Form["check"] != "0") return new OkObjectResult("");

        var (newUser, errors) = await userAuthService.RegisterUser(username, password, email, ip);

        var noErrorsFound = errors is { Count: 0 };

        if (newUser == null && errors == null || noErrorsFound)
        {
            errors ??= new Dictionary<string, List<string>>
            {
                ["username"] = []
            };

            errors["username"].Add("Unknown error");
        }

        if (errors != null && errors.Any(x => x.Value.Count > 0))
            return new BadRequestObjectResult(new
            {
                form_error = new
                {
                    user = errors
                }
            });

        return new OkObjectResult("");
    }
}