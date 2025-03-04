using System.Net;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Objects.Sessions;

public class BaseSession(User user, bool isGuest = false)
{
    public int UserId { get; } = user.Id;
    public bool IsGuest { get; } = isGuest;

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

    public static BaseSession GenerateGuestSession(IPAddress ip)
    {
        var user = new User
        {
            Id = ip.GetHashCode(),
            Username = "Guest"
        };

        return new BaseSession(user, true);
    }
}