using System.Net;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Helpers;

public static class RegionHelper
{
    private const string Api = "http://ip-api.com/json/";

    public static async Task<Location> GetRegion(IPAddress ip)
    {
        var location = await RequestsHelper.SendRequest<Location>($"{Api}{ip}") ?? new Location();
        location.Ip = ip.ToString();

        return location;
    }

    public static IPAddress GetUserIpAddress(HttpRequest request)
    {
        var ipAddress = string.Empty;

        string? xForwardedFor = request.Headers["X-Forwarded-For"];

        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            var ipAddresses = xForwardedFor.Split([
                    ','
                ],
                StringSplitOptions.RemoveEmptyEntries);

            if (ipAddresses.Length > 0) ipAddress = ipAddresses[0].Trim();
        }

        if (string.IsNullOrEmpty(ipAddress)) ipAddress = request.HttpContext.Connection.RemoteIpAddress?.ToString();

        return IPAddress.TryParse(ipAddress, out var ip) ? ip : IPAddress.Loopback;
    }

    public static bool IsFromDocker(this IPAddress ip)
    {
        return ip.MapToIPv4().ToString().StartsWith("172.");
    }

    public static bool IsFromLocalNetwork(this IPAddress ip)
    {
        return ip.Equals(IPAddress.Loopback);
    }

    public static short GetCountryCode(string cc)
    {
        if (Enum.TryParse(typeof(CountryCodes), cc, true, out var result)) return (short)result;

        return 0;
    }
}