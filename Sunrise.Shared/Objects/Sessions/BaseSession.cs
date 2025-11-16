using System.Net;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Objects.Sessions;

public class BaseSession(User user, string ipAddress, bool isGuest = false, bool isServer = false)
{
    public int UserId { get; } = user.Id;
    public bool IsGuest { get; } = isGuest;
    public bool IsServer { get; } = isServer;
    public string IpAddress { get; } = ipAddress;

    public bool IsRateLimited()
    {
        var rateLimits = ServicesProviderHolder.GetRequiredService<RateLimitRepository>();
        return rateLimits.IsRateLimited(this) && !IsServer;
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

        return new BaseSession(user, ip.ToString(), true);
    }

    public static BaseSession GenerateServerSession()
    {
        var user = new User
        {
            Id = int.MaxValue,
            Username = "Server"
        };

        return new BaseSession(user, IPAddress.Loopback.ToString(), true, true);
    }
}