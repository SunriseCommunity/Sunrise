using System.Collections.Concurrent;
using Sunrise.Server.Objects;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Repositories;

public class RateLimitRepository
{
    private readonly int _rateLimit = Configuration.UserApiCallsInMinute;
    private readonly ConcurrentDictionary<int, RateLimiter> _rateLimits = new();

    public bool IsRateLimited(BaseSession session)
    {
        if (_rateLimits.TryGetValue(session.User.Id, out var rateLimiter))
            return !rateLimiter.CanSend(session);

        rateLimiter = new RateLimiter(_rateLimit, TimeSpan.FromMinutes(1), false, false);
        _rateLimits.TryAdd(session.User.Id, rateLimiter);

        return !rateLimiter.CanSend(session);
    }

    public int GetRemainingCalls(BaseSession session)
    {
        return _rateLimits.TryGetValue(session.User.Id, out var rateLimiter) ? rateLimiter.GetRemainingCalls(session) : _rateLimit;
    }
}