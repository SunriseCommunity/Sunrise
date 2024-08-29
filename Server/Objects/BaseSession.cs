using Sunrise.Server.Database.Models;
using Sunrise.Server.Repositories;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects;

public class BaseSession(User user)
{
    public User User { get; private set; } = user;

    public bool IsRateLimited()
    {
        var rateLimits = ServicesProviderHolder.GetRequiredService<RateLimitRepository>();
        return rateLimits.IsRateLimited(this);
    }

    public int GetRemainingCalls()
    {
        var rateLimits = ServicesProviderHolder.GetRequiredService<RateLimitRepository>();
        return rateLimits.GetRemainingCalls(this);
    }
}