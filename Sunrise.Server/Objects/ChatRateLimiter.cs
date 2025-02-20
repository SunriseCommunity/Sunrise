using HOPEless.Bancho;
using Sunrise.Server.Application;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Types.Enums;
using Sunrise.Shared.Types.Interfaces;

namespace Sunrise.Server.Objects;

public class ChatRateLimiter(int messagesLimit, TimeSpan timeWindow, bool actionOnLimit = true, bool ignoreMods = true) : RateLimiter(messagesLimit, timeWindow)
{
    public new bool CanSend(BaseSession session)
    {
        if (session.User.Privilege.HasFlag(UserPrivileges.Admin) && ignoreMods) return true;

        var isCanSend = base.CanSend(session);

        if (!isCanSend)
        {
            if (actionOnLimit && session is Session gameSession)
                _ = SilenceUser(gameSession);
        }

        return isCanSend;
    }

    private static async Task SilenceUser(Session session)
    {
        var silenceTime = TimeSpan.FromMinutes(5);
        session.User.SilencedUntil = DateTime.UtcNow + silenceTime;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.UpdateUser(session
            .User); // NOTE: I have no guarantee that we will not overwrite some db changes here. Just pointing out.

        var sessions = ServicesProviderHolder.GetRequiredService<ISessionRepository>();
        session.SendSilenceStatus((int)silenceTime.TotalSeconds, "You are sending messages too fast. Slow down!");

        sessions.WriteToAllSessions(PacketType.ServerUserSilenced, session.User.Id);
    }
}