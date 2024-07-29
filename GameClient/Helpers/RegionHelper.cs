using System.Text.Json;
using Sunrise.GameClient.Objects.Serializable;
using Sunrise.GameClient.Types.Enums;

namespace Sunrise.GameClient.Helpers;

public class RegionHelper
{
    private const string ApiBase = "https://ip.zxq.co/";
    private Location Location { get; set; } = new Location();

    public async Task<Location> GetRegion(string ip)
    {
        var client = new HttpClient();
        var response = await client.GetAsync(ApiBase + ip);
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(content))
            return Location;

        var location = JsonSerializer.Deserialize<Location?>(content);
        if (location != null)
            Location = location;

        return Location;
    }

    public string GetUserIpAddress(HttpRequest request)
    {
        string ipAddress = string.Empty;

        string xForwardedFor = request.Headers["X-Forwarded-For"];
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            var ipAddresses = xForwardedFor.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (ipAddresses.Length > 0)
            {
                ipAddress = ipAddresses[0].Trim();
            }
        }

        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = request.HttpContext.Connection.RemoteIpAddress.ToString();
        }

        return ipAddress;
    }

    public short GetCountryCode(string cc)
    {
        if (Enum.TryParse(typeof(CountryCodes), cc, true, out var result))
        {
            return (short)result;
        }

        return 0;
    }
}