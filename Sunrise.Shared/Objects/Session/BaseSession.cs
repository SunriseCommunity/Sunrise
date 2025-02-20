using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Objects.Session;

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