using System.Net;
using System.Text.Json;

namespace Sunrise.Server.Helpers;

public class RequestsHelper
{
    private static readonly ILogger<RequestsHelper> Logger;
    private static readonly HttpClient Client = new();

    static RequestsHelper()
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        Logger = loggerFactory.CreateLogger<RequestsHelper>();
    }

    public static async Task<T?> SendRequest<T>(string requestUri, int requestTry = 0)
    {
        var response = await Client.GetAsync(requestUri);

        if (response.StatusCode.Equals(HttpStatusCode.TooManyRequests))
        {
            Logger.LogWarning($"Request to {requestUri} failed with status code {response.StatusCode}");

            if (requestTry < 3)
            {
                await Task.Delay(2000);
                Logger.LogInformation($"Retrying request to {requestUri} (try {requestTry + 1})");
                return await SendRequest<T>(requestUri, requestTry + 1);
            }

            return default;
        }

        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        if (typeof(T) == typeof(byte[]))
        {
            return (T)(object)await response.Content.ReadAsByteArrayAsync();
        }

        var content = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(content);
    }
}