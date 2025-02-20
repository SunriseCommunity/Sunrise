using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Types.Interfaces;

namespace Sunrise.Shared.Objects;

public class BaseSession(User user) : IBaseSession
{
    public User User { get; } = user;
    public string Token { get; } = Guid.NewGuid().ToString();

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