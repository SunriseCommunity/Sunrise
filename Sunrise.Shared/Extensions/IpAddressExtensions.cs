using System.Net;

namespace Sunrise.Shared.Extensions;

public static class IpAddressExtensions
{
    public static bool IsFromDocker(this IPAddress ip)
    {
        return ip.MapToIPv4().ToString().StartsWith("172.");
    }

    public static bool IsFromLocalNetwork(this IPAddress ip)
    {
        return ip.Equals(IPAddress.Loopback);
    }
}