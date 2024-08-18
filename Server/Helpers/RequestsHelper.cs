using System.Net;
using System.Text.Json;
using Sunrise.Server.Data;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

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

    public static async Task<T?> SendRequest<T>(Session session, ApiType type, object?[] args)
    {
        if (await session.IsRateLimited())
        {
            Logger.LogWarning($"User {session.User.Id} got rate limited. Ignoring request.");
            return default;
        }

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var apis = await database.GetExternalApis(type);

        if (apis is { Count: 0 } or null)
        {
            Logger.LogWarning($"No API servers found for {type}.");
            return default;
        }

        foreach (var api in apis)
        {
            if (args.Length < api.NumberOfRequiredArgs)
            {
                Logger.LogWarning($"Not enough arguments for {type} for {api}. Required {api.NumberOfRequiredArgs}, got {args.Length}.");
                continue;
            }

            var requestUri = string.Format(api.Url, args);

            SunriseMetrics.ExternalApiRequestsCounterInc(type, api.Server, session);

            var (response, isServerRateLimited) = await SendApiRequest<T>(api.Server, requestUri);

            if (isServerRateLimited)
            {
                continue;
            }

            if (response is not null)
            {
                return response;
            }
        }

        Logger.LogWarning($"Failed to get response from any API server for {type} with args {string.Join(", ", args)}.");

        return default;
    }

    [Obsolete("Use only only if we can't get user session.")]
    public static async Task<T?> SendRequest<T>(string requestUri, int requestTry = 0)
    {
        var response = await Client.GetAsync(requestUri);

        // TODO: Can be refactored to has multiple urls to try and also cache the response.

        if (response.StatusCode.Equals(HttpStatusCode.TooManyRequests))
        {
            Logger.LogWarning($"Request to {requestUri} failed with status code {response.StatusCode}");

            if (requestTry >= 3)
                return default;

            await Task.Delay(2000);
            Logger.LogInformation($"Retrying request to {requestUri} (try {requestTry + 1})");
            return await SendRequest<T>(requestUri, requestTry + 1);

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

    private static async Task<(T?, bool)> SendApiRequest<T>(ApiServer server, string requestUri)
    {
        var redis = ServicesProviderHolder.ServiceProvider.GetRequiredService<RedisRepository>();
        var isServerRateLimited = await redis.Get<bool?>(RedisKey.ApiServerRateLimited(server));

        if (isServerRateLimited is true)
        {
            Logger.LogWarning($"Server {server} is rate limited. Ignoring request.");
            return (default, true);
        }

        var response = await Client.GetAsync(requestUri);
        var rateLimit = string.Empty;
        var rateLimitReset = "60";

        switch (server)
        {
            case ApiServer.Ppy:
            case ApiServer.CatboyBest:
                rateLimit = response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
                break;
            case ApiServer.OsuDirect:
                rateLimit = response.Headers.GetValues("RateLimit-Remaining").FirstOrDefault();
                rateLimitReset = response.Headers.GetValues("RateLimit-Reset").FirstOrDefault() ?? "60";
                break;
            case ApiServer.OldPpy:
                break;
            case ApiServer.Nerinyan:
            case ApiServer.OsuOkayu:
            default:
                Logger.LogWarning($"Server {server} rate limit headers wasn't set. Ignoring rate limit.");
                break;
        }

        if (rateLimit is not null && int.TryParse(rateLimit, out var rateLimitInt) && rateLimitInt <= 5)
        {
            await redis.Set(RedisKey.ApiServerRateLimited(server), true, TimeSpan.FromSeconds(int.Parse(rateLimitReset)));
        }

        if (response.StatusCode.Equals(HttpStatusCode.TooManyRequests))
        {
            Logger.LogWarning($"Request to {server} failed with status code {response.StatusCode}. Rate limiting server for 10 minutes.");

            await redis.Set(RedisKey.ApiServerRateLimited(server), true, TimeSpan.FromMinutes(10));

            return (default, true);
        }

        if (!response.IsSuccessStatusCode)
        {
            return (default, false);
        }

        switch (typeof(T))
        {
            case not null when typeof(T) == typeof(byte[]):
                return ((T)(object)await response.Content.ReadAsByteArrayAsync(), false);
            case not null when typeof(T) == typeof(string):
                return ((T)(object)await response.Content.ReadAsStringAsync(), false);
            default:
                var content = await response.Content.ReadAsStringAsync();
                return (JsonSerializer.Deserialize<T>(content), false);
        }
    }
}