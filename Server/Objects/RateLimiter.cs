using HOPEless.Bancho;
using Sunrise.Server.Database;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects;

public class RateLimiter(int messagesLimit, TimeSpan timeWindow, bool actionOnLimit = true, bool ignoreMods = true)
{
    private readonly Dictionary<int, List<DateTime>> _requestTimestamps = new();

    public bool CanSend(BaseSession session)
    {
        var userId = session.User.Id;
        var now = DateTime.UtcNow;

        if (session.User.Privilege.HasFlag(UserPrivileges.Admin) && ignoreMods)
        {
            return true;
        }

        if (!_requestTimestamps.ContainsKey(userId))
        {
            _requestTimestamps[userId] = [];
        }

        var timestamps = _requestTimestamps[userId];
        timestamps.RemoveAll(t => now - t > timeWindow);

        if (timestamps.Count >= messagesLimit)
        {
            if (actionOnLimit && session is Session gameSession)
                _ = SilenceUser(gameSession);

            return false;
        }

        timestamps.Add(now);
        return true;
    }

    public int GetRemainingCalls(BaseSession session)
    {
        var userId = session.User.Id;
        var now = DateTime.UtcNow;

        if (!_requestTimestamps.ContainsKey(userId))
        {
            _requestTimestamps[userId] = [];
        }

        var timestamps = _requestTimestamps[userId];
        timestamps.RemoveAll(t => now - t > timeWindow);

        return messagesLimit - timestamps.Count;
    }

    private static async Task SilenceUser(Session session)
    {
        var silenceTime = TimeSpan.FromMinutes(5);
        session.User.SilencedUntil = DateTime.UtcNow + silenceTime;

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        await database.UpdateUser(session.User); // NOTE: I have no guarantee that we will not overwrite some db changes here. Just pointing out.

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        session.SendSilenceStatus((int)silenceTime.TotalSeconds, "You are sending messages too fast. Slow down!");

        sessions.WriteToAllSessions(PacketType.ServerUserSilenced, session.User.Id);
    }
}