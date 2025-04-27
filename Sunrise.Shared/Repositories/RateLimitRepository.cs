using System.Collections.Concurrent;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using RateLimiter = Sunrise.Shared.Objects.RateLimiter;

namespace Sunrise.Shared.Repositories;

public class RateLimitRepository
{
    private readonly ConcurrentDictionary<int, RateLimiter> _rateLimits = new();

    public bool IsRateLimited(BaseSession session)
    {
        if (_rateLimits.TryGetValue(session.UserId, out var rateLimiter))
            return !rateLimiter.CanSend(session);

        rateLimiter = new RateLimiter(Configuration.ApiCallsPerWindow,
            TimeSpan.FromSeconds(Configuration.ApiWindow));
        _rateLimits.TryAdd(session.UserId, rateLimiter);

        return !rateLimiter.CanSend(session);
    }

    public int GetRemainingCalls(BaseSession session)
    {
        return _rateLimits.TryGetValue(session.UserId, out var rateLimiter)
            ? rateLimiter.GetRemainingCalls(session)
            : Configuration.ApiCallsPerWindow;
    }
}