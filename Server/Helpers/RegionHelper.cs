using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Helpers;

public static class RegionHelper
{
    private const string Api = "http://ip-api.com/json/";

    public static async Task<Location> GetRegion(string ip)
    {
        var location = await RequestsHelper.SendRequest<Location>($"{Api}{ip}") ?? new Location();
        location.Ip = ip;

        return location;
    }

    public static string GetUserIpAddress(HttpRequest request)
    {
        var ipAddress = string.Empty;

        string? xForwardedFor = request.Headers["X-Forwarded-For"];

        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            var ipAddresses = xForwardedFor.Split(new[]
                {
                    ','
                },
                StringSplitOptions.RemoveEmptyEntries);

            if (ipAddresses.Length > 0)
            {
                ipAddress = ipAddresses[0].Trim();
            }
        }

        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = request.HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        return ipAddress ?? string.Empty;
    }

    public static short GetCountryCode(string cc)
    {
        if (Enum.TryParse(typeof(CountryCodes), cc, true, out var result))
        {
            return (short)result;
        }

        return 0;
    }
}