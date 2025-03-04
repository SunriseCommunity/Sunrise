using System.Net;
using Microsoft.AspNetCore.Http;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Services;

public class RegionService(RedisRepository redisRepository, HttpClientService client)
{
    public async Task<Location> GetRegion(IPAddress ip)
    {
        var cachedRegion = await redisRepository.Get<Location>(RedisKey.LocationFromIp(ip.ToString()));

        if (cachedRegion != null)
        {
            return cachedRegion;
        }

        var guestSession = BaseSession.GenerateGuestSession(ip);
        var location = await client.SendRequest<Location>(guestSession, ApiType.GetIPLocation, [ip.ToString()]) ?? new Location();

        location.Ip = ip.ToString();

        await redisRepository.Set(RedisKey.LocationFromIp(location.Ip), location);

        return location;
    }

    public static IPAddress GetUserIpAddress(HttpRequest request)
    {
        var ipAddress = string.Empty;

        string? xForwardedFor = request.Headers["X-Forwarded-For"];

        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            var ipAddresses = xForwardedFor.Split([","], StringSplitOptions.RemoveEmptyEntries);

            if (ipAddresses.Length > 0) ipAddress = ipAddresses[0].Trim();
        }

        if (string.IsNullOrEmpty(ipAddress)) ipAddress = request.HttpContext.Connection.RemoteIpAddress?.ToString();

        return IPAddress.TryParse(ipAddress, out var ip) ? ip : IPAddress.Loopback;
    }

    public static short GetCountryCode(string cc)
    {
        if (Enum.TryParse(typeof(CountryCode), cc, true, out var result)) return (short)result;

        return 0;
    }
}