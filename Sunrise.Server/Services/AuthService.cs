using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sunrise.Server.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Services;

public class AuthService(DatabaseService database, SessionRepository sessions, UserAuthService userAuthService, UserService userService)
{
    public async Task<FileContentResult> Login(HttpRequest request, HttpResponse response)
    {
        var sr = await new StreamReader(request.Body).ReadToEndAsync();
        var loginRequest = ServerParsers.ParseLogin(sr);
        var ip = RegionService.GetUserIpAddress(request);

        response.Headers["cho-protocol"] = "19";
        response.Headers.Connection = "keep-alive";

        var (session, error, loginResponseCode) = await userService.GetNewUserSession(loginRequest, ip);

        if (error != null || session == null)
            return RejectLogin(response, error, loginResponseCode);

        var addEventResult = await database.Events.Users.AddUserLoginEvent(session.UserId, ip.ToString(), true, sr);
        if (addEventResult.IsFailure)
            return RejectLogin(response, addEventResult.Error);

        response.Headers["cho-token"] = session.Token;

        if (request.Headers["X-Using-Old-Domain"] == "true")
            return RejectLogin(response, $"You are using old domain name, please try to connect with:\n \"-devserver {Configuration.Domain}\".\nIf you have any problems with connection, contact staff at discord.");

        return await ProceedWithLogin(session, response);
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

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    private async Task<FileContentResult> ProceedWithLogin(Session session, HttpResponse response)
    {
        session.SendLoginResponse(LoginResponse.Success);
        session.SendProtocolVersion();
        session.SendPrivilege();
        session.SendExistingChannels();

        var chatChannels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();

        chatChannels.JoinChannel("#osu", session);
        chatChannels.JoinChannel("#announce", session);

        var sessionUser = await database.Users.GetUser(session.UserId,
            options: new QueryOptions
            {
                QueryModifier = q => q.Cast<User>().Include(u => u.UserInitiatedRelationships)
            });
        if (sessionUser == null)
            return RejectLogin(response, "User for this session doesn't exist");

        if (sessionUser.Privilege.HasFlag(UserPrivilege.Admin)) chatChannels.JoinChannel("#staff", session);

        foreach (var channel in chatChannels.GetChannels(session))
        {
            session.SendChannelAvailable(channel);
        }

        var friends = sessionUser.UserInitiatedRelationships.Where(r => r.Relation == UserRelation.Friend).Select(r => r.TargetId).ToList();
        session.SendFriendsList(friends);

        await sessions.SendCurrentPlayers(session);

        if (sessionUser.SilencedUntil > DateTime.UtcNow)
            session.SendSilenceStatus((int)(sessionUser.SilencedUntil - DateTime.UtcNow).TotalSeconds);

        sessions.WriteToAllSessions(PacketType.ServerUserPresence, await session.Attributes.GetPlayerPresence());
        sessions.WriteToAllSessions(PacketType.ServerUserData, await session.Attributes.GetPlayerData());

        await session.SendUserData();
        await session.SendUserPresence();

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