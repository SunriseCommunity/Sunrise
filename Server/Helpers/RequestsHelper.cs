using System.Net;
using System.Text.Json;
using System.Web;
using Sunrise.Server.Application;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Helpers;

public class RequestsHelper
{
    private static readonly ILogger<RequestsHelper> Logger;
    private static readonly HttpClient Client = new();

    static RequestsHelper()
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        Logger = loggerFactory.CreateLogger<RequestsHelper>();

        Client.DefaultRequestHeaders.Add("Accept", "application/json");
        Client.DefaultRequestHeaders.Add("User-Agent", "Sunrise");
    }

    public static async Task<T?> SendRequest<T>(BaseSession session, ApiType type, object?[] args)
    {
        if (session.IsRateLimited())
        {
            Logger.LogWarning($"User {session.User.Id} got rate limited. Ignoring request.");
            return default;
        }

        var apis = Configuration.ExternalApis.Where(x => x.Type == type).OrderBy(x => x.Priority).ToList();

        if (apis is { Count: 0 } or null)
        {
            Logger.LogWarning($"No API servers found for {type}.");
            return default;
        }

        foreach (var api in apis)
        {
            if (args.Length < api.NumberOfRequiredArgs)
            {
                Logger.LogWarning(
                    $"Not enough arguments for {type} for {api}. Required {api.NumberOfRequiredArgs}, got {args.Length}.");
                continue;
            }

            var requestUri = string.Format(api.Url, args);
            requestUri = SerializeUrlQuery(requestUri);

            SunriseMetrics.ExternalApiRequestsCounterInc(type, api.Server, session);

            var (response, isServerRateLimited) = await SendApiRequest<T>(api.Server, requestUri);

            if (isServerRateLimited) continue;

            if (response is not null) return response;
        }

        Logger.LogWarning(
            $"Failed to get response from any API server for {type} with args {string.Join(", ", args)}.");

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

        if (!response.IsSuccessStatusCode) return default;

        if (typeof(T) == typeof(byte[])) return (T)(object)await response.Content.ReadAsByteArrayAsync();

        var content = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(content);
    }

    private static async Task<(T?, bool)> SendApiRequest<T>(ApiServer server, string requestUri)
    {
        var redis = ServicesProviderHolder.GetRequiredService<RedisRepository>();
        var isServerRateLimited = await redis.Get<bool?>(RedisKey.ApiServerRateLimited(server));

        if (isServerRateLimited is true)
        {
            Logger.LogWarning($"Server {server} is rate limited. Ignoring request.");
            return (default, true);
        }

        try
        {
            HttpResponseMessage response;

            if (server == ApiServer.Observatory)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

                if (!string.IsNullOrEmpty(Configuration.ObservatoryApiKey))
                    request.Headers.Add("Authorization", $"{Configuration.ObservatoryApiKey}");

                response = await Client.SendAsync(request);
            }
            else
            {
                response = await Client.GetAsync(requestUri);
            }

            var rateLimit = string.Empty;
            var rateLimitReset = "60";
            IEnumerable<string>? rateLimitHeader;

            switch (server)
            {
                case ApiServer.Ppy:
                case ApiServer.CatboyBest:
                    rateLimit = response.Headers.TryGetValues("X-RateLimit-Remaining", out rateLimitHeader)
                        ? rateLimitHeader.FirstOrDefault()
                        : "60";
                    break;
                case ApiServer.OsuDirect:
                    rateLimit = response.Headers.TryGetValues("RateLimit-Remaining", out rateLimitHeader)
                        ? rateLimitHeader.FirstOrDefault()
                        : "60";
                    rateLimitReset = response.Headers.TryGetValues("RateLimit-Reset", out rateLimitHeader)
                        ? rateLimitHeader.FirstOrDefault()
                        : "60";
                    break;
                case ApiServer.OldPpy:
                case ApiServer.Nerinyan:
                case ApiServer.Observatory:
                    break;
                default:
                    Logger.LogWarning($"Server {server} rate limit headers wasn't set. Ignoring rate limit.");
                    break;
            }

            if (rateLimit is not null && int.TryParse(rateLimit, out var rateLimitInt) && rateLimitInt <= 5)
                await redis.Set(RedisKey.ApiServerRateLimited(server),
                    true,
                    TimeSpan.FromSeconds(int.TryParse(rateLimitReset, out var rateLimitResetInt)
                        ? rateLimitResetInt
                        : 60));

            if (response.StatusCode.Equals(HttpStatusCode.TooManyRequests))
            {
                Logger.LogWarning(
                    $"Request to {server} failed with status code {response.StatusCode}. Rate limiting server for 1 minute.");

                await redis.Set(RedisKey.ApiServerRateLimited(server), true, TimeSpan.FromMinutes(1));

                return (default, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode < HttpStatusCode.BadGateway)
                    return (default, false);

                Logger.LogWarning(
                    $"{server} returned status code {response.StatusCode}. Ignoring server for 10 minutes.");
                await redis.Set(RedisKey.ApiServerRateLimited(server), true, TimeSpan.FromMinutes(10));

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
        catch (Exception e)
        {
            Logger.LogError(e, $"Failed to process request to {server} with uri {requestUri}");
            return (default, false);
        }
    }

    private static string SerializeUrlQuery(string url)
    {
        var results = HttpUtility.ParseQueryString(url);
        var nonEmpty = new Dictionary<string, string>();

        foreach (var k in results.AllKeys)
        {
            if (!string.IsNullOrWhiteSpace(results[k]))
            {
                nonEmpty.Add(k, results[k]);
            }
        }

        return string.Join("&", nonEmpty.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }
}