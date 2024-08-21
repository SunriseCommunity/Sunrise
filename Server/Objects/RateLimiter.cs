using HOPEless.Bancho;
using osu.Shared;
using Sunrise.Server.Repositories;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects;

public class RateLimiter(int messagesLimit, TimeSpan timeWindow)
{
    private readonly Dictionary<int, List<DateTime>> _messageTimestamps = new();

    public bool CanSend(Session session, bool actionOnLimit = true, bool ignoreMods = true)
    {
        var userId = session.User.Id;
        var now = DateTime.UtcNow;

        if (session.User.Privilege >= PlayerRank.SuperMod && ignoreMods)
        {
            return true;
        }

        if (!_messageTimestamps.ContainsKey(userId))
        {
            _messageTimestamps[userId] = [];
        }

        var timestamps = _messageTimestamps[userId];
        timestamps.RemoveAll(t => now - t > timeWindow);

        if (timestamps.Count >= messagesLimit)
        {
            if (actionOnLimit)
            {
                SilenceUser(session);
            }

            return false;
        }

        timestamps.Add(now);
        return true;
    }

    private static void SilenceUser(Session session)
    {
        var silenceTime = TimeSpan.FromMinutes(5);
        session.User.SilencedUntil = DateTime.UtcNow + silenceTime;

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();
        session.SendSilenceStatus((int)silenceTime.TotalSeconds, "You are sending messages too fast. Slow down!");

        sessions.WriteToAllSessions(PacketType.ServerUserSilenced, session.User.Id);
    }
}