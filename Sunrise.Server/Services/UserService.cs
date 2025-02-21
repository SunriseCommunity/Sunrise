using System.Net;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Services;

public class UserService(RegionService regionService)
{
    public async Task<(Session?, string?, LoginResponse)> GetNewUserSession(LoginRequest loginRequest, IPAddress ip)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(username: loginRequest.Username);

        if (user == null)
            return (null, "User with this username does not exist.", LoginResponse.InvalidCredentials);

        if (user.Passhash != loginRequest.PassHash)
            return (null, "Invalid credentials.", LoginResponse.InvalidCredentials);

        if (Configuration.OnMaintenance && !user.Privilege.HasFlag(UserPrivilege.Admin))
            return (null,
                "Server is currently in maintenance mode. Please try again later.",
                LoginResponse.ServerError);

        if (user.IsRestricted() && await database.UserService.Moderation.IsRestricted(user.Id))
            return (null, "Your account is restricted. Please contact support for more information.", LoginResponse.InvalidCredentials);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var oldSession = sessions.GetSession(userId: user.Id);

        if (oldSession != null)
        {
            oldSession.SendNotification("You have been logged in from another location. Please try again later.");
            sessions.SoftRemoveSession(oldSession);
        }

        var location = await regionService.GetRegion(ip);
        location.TimeOffset = loginRequest.UtcOffset;

        var session = sessions.CreateSession(user, location, loginRequest);

        if (user.AccountStatus == UserAccountStatus.Disabled)
        {
            await database.UserService.Moderation.EnableUser(user.Id);
            session.SendNotification("Welcome back! Your account has been re-enabled. It may take a few seconds to load your data.");
        }

        return (session, null, LoginResponse.Success);
    }

    public async Task<string?> GetFriends(string username)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(username: username);

        if (user == null)
            return null;

        var friends = user.FriendsList;

        return string.Join("\n", friends);
    }
}