using HOPEless.Bancho;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Objects.Chat;

public class ChatRateLimiter(int messagesLimit, TimeSpan timeWindow, bool actionOnLimit = true, bool ignoreMods = true) : RateLimiter(messagesLimit, timeWindow)
{
    private readonly Dictionary<int, List<DateTime>> _requestTimestamps = new();

    public new bool CanSend(BaseSession session)
    {
        var canSend = base.CanSend(session);
        
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        
        var user = database.Users
            .GetUser(id: session.UserId, options: new QueryOptions(true))
            .ConfigureAwait(false).GetAwaiter().GetResult();
        
        if (user == null)
            return false;

        if (user.Privilege.HasFlag(UserPrivilege.Admin) && ignoreMods) return true;

        if (!canSend)
        {
            if (actionOnLimit && session is Sessions.Session gameSession)
                _ = SilenceUser(gameSession);
        }

        return canSend;
    }

    private static async Task SilenceUser(Sessions.Session session)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = await database.Users.GetUser(id: session.UserId);
        
        if (user == null)
            return;
        
        var silenceTime = TimeSpan.FromMinutes(5);
        user.SilencedUntil = DateTime.UtcNow + silenceTime;

        await database.Users.UpdateUser(user);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        session.SendSilenceStatus((int)silenceTime.TotalSeconds, "You are sending messages too fast. Slow down!");

        sessions.WriteToAllSessions(PacketType.ServerUserSilenced, session.UserId);
    }
}