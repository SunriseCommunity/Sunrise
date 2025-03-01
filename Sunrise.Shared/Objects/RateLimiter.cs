using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Objects;

public class RateLimiter(int messagesLimit, TimeSpan timeWindow)
{
    private readonly Dictionary<int, List<DateTime>> _requestTimestamps = new();

    public bool CanSend(BaseSession session)
    {
        var userId = session.UserId;
        var now = DateTime.UtcNow;

        if (!_requestTimestamps.ContainsKey(userId)) _requestTimestamps[userId] = [];

        var timestamps = _requestTimestamps[userId];
        timestamps.RemoveAll(t => now - t > timeWindow);

        if (timestamps.Count >= messagesLimit)
        {
            return false;
        }

        timestamps.Add(now);
        return true;
    }

    public int GetRemainingCalls(BaseSession session)
    {
        var userId = session.UserId;
        var now = DateTime.UtcNow;

        if (!_requestTimestamps.ContainsKey(userId)) _requestTimestamps[userId] = [];

        var timestamps = _requestTimestamps[userId];
        timestamps.RemoveAll(t => now - t > timeWindow);

        return messagesLimit - timestamps.Count;
    }
}