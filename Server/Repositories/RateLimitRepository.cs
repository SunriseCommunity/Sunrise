using System.Collections.Concurrent;
using Sunrise.Server.Application;
using Sunrise.Server.Objects;

namespace Sunrise.Server.Repositories;

public class RateLimitRepository
{
    private readonly ConcurrentDictionary<int, RateLimiter> _rateLimits = new();

    public bool IsRateLimited(BaseSession session)
    {
        if (_rateLimits.TryGetValue(session.User.Id, out var rateLimiter))
            return !rateLimiter.CanSend(session);

        rateLimiter = new RateLimiter(Configuration.ApiCallsPerWindow, TimeSpan.FromSeconds(Configuration.ApiWindow),
            false, false);
        _rateLimits.TryAdd(session.User.Id, rateLimiter);

        return !rateLimiter.CanSend(session);
    }

    public int GetRemainingCalls(BaseSession session)
    {
        return _rateLimits.TryGetValue(session.User.Id, out var rateLimiter)
            ? rateLimiter.GetRemainingCalls(session)
            : Configuration.ApiCallsPerWindow;
    }
}