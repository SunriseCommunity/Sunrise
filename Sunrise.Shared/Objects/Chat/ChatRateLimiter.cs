using HOPEless.Bancho;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Objects.Chat;

public class ChatRateLimiter(int messagesLimit, TimeSpan timeWindow, bool actionOnLimit = true, bool ignoreMods = true) : RateLimiter(messagesLimit, timeWindow)
{
    private readonly Dictionary<int, List<DateTime>> _requestTimestamps = new();

    public new bool CanSend(BaseSession session)
    {
        var canSend = base.CanSend(session);

        if (session.User.Privilege.HasFlag(UserPrivilege.Admin) && ignoreMods) return true;

        if (!canSend)
        {
            if (actionOnLimit && session is Session.Session gameSession)
                _ = SilenceUser(gameSession);
        }

        return canSend;
    }

    private static async Task SilenceUser(Session.Session session)
    {
        var silenceTime = TimeSpan.FromMinutes(5);
        session.User.SilencedUntil = DateTime.UtcNow + silenceTime;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.UpdateUser(session
            .User); // NOTE: I have no guarantee that we will not overwrite some db changes here. Just pointing out.

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        session.SendSilenceStatus((int)silenceTime.TotalSeconds, "You are sending messages too fast. Slow down!");

        sessions.WriteToAllSessions(PacketType.ServerUserSilenced, session.User.Id);
    }
}