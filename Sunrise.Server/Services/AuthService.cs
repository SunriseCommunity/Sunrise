using HOPEless.Bancho;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Helpers.Requests;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Services;

public class AuthService
{
    private readonly UserAuthService _userAuthService = new();

    public async Task<IActionResult> Login(HttpRequest request, HttpResponse response)
    {
        var sr = await new StreamReader(request.Body).ReadToEndAsync();
        var loginRequest = ServerParsers.ParseLogin(sr);
        var ip = RegionHelper.GetUserIpAddress(request);

        response.Headers["cho-protocol"] = "19";
        response.Headers.Connection = "keep-alive";

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(username: loginRequest.Username);

        if (user == null)
            return RejectLogin(response, "User with this username does not exist.");

        if (user.Passhash != loginRequest.PassHash)
            return RejectLogin(response, "Invalid credentials.");

        if (Configuration.OnMaintenance && !user.Privilege.HasFlag(UserPrivilege.Admin))
            return RejectLogin(response,
                "Server is currently in maintenance mode. Please try again later.",
                LoginResponse.ServerError);

        if (user.IsRestricted() && await database.UserService.Moderation.IsRestricted(user.Id))
            return RejectLogin(response, "Your account is restricted. Please contact support for more information.");

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var oldSession = sessions.GetSession(userId: user.Id);

        if (oldSession != null)
        {
            oldSession.SendNotification("You have been logged in from another location. Please try again later.");
            sessions.SoftRemoveSession(oldSession);
        }

        var location = await RegionHelper.GetRegion(ip);
        location.TimeOffset = loginRequest.UtcOffset;

        await database.EventService.UserEvent.CreateNewUserLoginEvent(user.Id, ip.ToString(), true, sr);


        var session = sessions.CreateSession(user, location, loginRequest);

        if (user.AccountStatus == UserAccountStatus.Disabled)
        {
            await database.UserService.Moderation.EnableUser(user.Id);
            session.SendNotification("Welcome back! Your account has been re-enabled. It may take a few seconds to load your data.");
        }

        response.Headers["cho-token"] = session.Token;

        return await ProceedWithLogin(session);
    }

    private IActionResult RejectLogin(HttpResponse response, string? reason = null,
        LoginResponse code = LoginResponse.InvalidCredentials)
    {
        response.Headers["cho-token"] = "no-token";

        var writer = new PacketHelper();

        if (reason != null)
            writer.WritePacket(PacketType.ServerNotification, reason);

        writer.WritePacket(PacketType.ServerLoginReply, code);

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    public IActionResult Relogin()
    {
        var writer = new PacketHelper();
        writer.WritePacket(PacketType.ServerRestart, 0); // Forces the client to relogin

        return new FileContentResult(writer.GetBytesToSend(), "application/octet-stream");
    }

    private static async Task<IActionResult> ProceedWithLogin(Session session)
    {
        session.SendLoginResponse(LoginResponse.Success);
        session.SendProtocolVersion();
        session.SendPrivilege();
        session.SendExistingChannels();

        var chatChannels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();

        chatChannels.JoinChannel("#osu", session);
        chatChannels.JoinChannel("#announce", session);

        if (session.User.Privilege.HasFlag(UserPrivilege.Admin)) chatChannels.JoinChannel("#staff", session);

        foreach (var channel in chatChannels.GetChannels(session))
        {
            session.SendChannelAvailable(channel);
        }

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        session.SendFriendsList();
        await sessions.SendCurrentPlayers(session);

        if (session.User.SilencedUntil > DateTime.UtcNow)
            session.SendSilenceStatus((int)(session.User.SilencedUntil - DateTime.UtcNow).TotalSeconds);

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

        var ip = RegionHelper.GetUserIpAddress(request);

        if (string.IsNullOrEmpty(ip.ToString()))
            return new BadRequestObjectResult("Invalid request: Missing IP address");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            return new BadRequestObjectResult("Invalid request: Missing parameters");

        if (request.Form["check"] != "0") return new OkObjectResult("");

        var (newUser, errors) = await _userAuthService.RegisterUser(username, password, email, ip);

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