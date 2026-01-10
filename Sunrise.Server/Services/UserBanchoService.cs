using System.Net;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Services;

public class UserBanchoService(DatabaseService database, SessionRepository sessions, RegionService regionService)
{

    public async Task<(User?, (string, LoginResponse)?)> GetUserFromLoginRequest(LoginRequest loginRequest, IPAddress ip)
    {
        var user = await database.Users.GetUser(username: loginRequest.Username,
            passhash: loginRequest.PassHash,
            options: new QueryOptions(true)
            {
                QueryModifier = q => q.Cast<User>()
                    .Include(u => u.UserStats)
                    .Include(u => u.UserInitiatedRelationships)
            });

        if (user == null)
            return (null, ("Invalid credentials.", LoginResponse.InvalidCredentials));

        return (user, null);
    }

    public async Task<(Session?, (string, LoginResponse)?)> GetNewUserSession(User user, LoginRequest loginRequest, IPAddress ip, Location? knownLocation = null)
    {
        if (Configuration.OnMaintenance && !user.Privilege.HasFlag(UserPrivilege.Admin))
            return (null,
                ("Server is currently in maintenance mode. Please try again later.",
                    LoginResponse.ServerError));

        if (user.IsUserSunriseBot())
            return (null, ("You can't login as Sunrise Bot", LoginResponse.InvalidCredentials));

        if (user.IsRestricted() && await database.Users.Moderation.IsUserRestricted(user.Id))
            return (null, ("Your account is restricted. Please contact support for more information.", LoginResponse.InvalidCredentials));

        var oldSession = sessions.GetSession(userId: user.Id);

        if (oldSession != null)
        {
            oldSession.SendNotification("You have been logged in from another location. Please try again later.");
            await sessions.RemoveSession(oldSession);
        }

        var location = knownLocation ?? await regionService.GetRegion(ip);
        location.TimeOffset = loginRequest.UtcOffset;

        var session = sessions.CreateSession(user, location, loginRequest);

        if (user.AccountStatus == UserAccountStatus.Disabled)
        {
            await database.Users.Moderation.EnableUser(user.Id);
            session.SendNotification("Welcome back! Your account has been re-enabled. It may take a few seconds to load your data.");
        }

        return (session, null);
    }

    public async Task<string?> GetFriends(int userId, CancellationToken ct = default)
    {
        var user = await database.Users.GetUser(userId,
            options: new QueryOptions
            {
                QueryModifier = q => q.Cast<User>().Include(u => u.UserInitiatedRelationships)
            },
            ct: ct);

        if (user == null)
            return null;

        var friends = user.UserInitiatedRelationships.Where(r => r.Relation == UserRelation.Friend).Select(r => r.TargetId).ToList();

        return string.Join("\n", friends);
    }
}