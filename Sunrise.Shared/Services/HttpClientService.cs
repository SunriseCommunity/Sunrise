using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Services;

public class HttpClientService
{
    private readonly HttpClient _client = new();
    private readonly ILogger<HttpClientService> _logger;
    private readonly RedisRepository _redis;

    public HttpClientService(RedisRepository redis)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<HttpClientService>();
        _redis = redis;

        _client.DefaultRequestHeaders.Add("Accept", "application/json");
        _client.DefaultRequestHeaders.Add("User-Agent", "Sunrise");
    }

    public async Task<T?> SendRequest<T>(BaseSession session, ApiType type, object?[] args, Dictionary<string, string>? headers = null)
    {
        if (session.IsRateLimited())
        {
            _logger.LogWarning($"User {session.UserId} got rate limited. Ignoring request.");
            return default;
        }

        headers ??= new Dictionary<string, string>();

        var apis = Configuration.ExternalApis.Where(x => x.Type == type).OrderBy(x => x.Priority).ToList();

        if (apis is { Count: 0 } or null)
        {
            _logger.LogWarning($"No API servers found for {type}.");
            return default;
        }

        foreach (var api in apis)
        {
            if (args.Length < api.NumberOfRequiredArgs)
            {
                _logger.LogWarning(
                    $"Not enough arguments for {type} for {api}. Required {api.NumberOfRequiredArgs}, got {args.Length}.");
                continue;
            }

            var requestUri = string.Format(api.Url, args);
            requestUri = SerializeUrlQuery(requestUri);

            SunriseMetrics.ExternalApiRequestsCounterInc(type, api.Server, session);

            if (api.Server == ApiServer.Observatory)
            {
                if (!string.IsNullOrEmpty(Configuration.ObservatoryApiKey))
                    headers.Add("Authorization", $"{Configuration.ObservatoryApiKey}");
            }

            var (response, isServerRateLimited) = await SendApiRequest<T>(api.Server, requestUri, headers);

            if (isServerRateLimited) continue;

            if (response is not null) return response;
        }

        _logger.LogWarning(
            $"Failed to get response from any API server for {type} with args {string.Join(", ", args)}.");

        return default;
    }

    [Obsolete("Use only only if we can't get user session.")]
    public async Task<T?> SendRequest<T>(string requestUri, int requestTry = 0)
    {
        var response = await _client.GetAsync(requestUri);

        // TODO: Can be refactored to has multiple urls to try and also cache the response.

        if (response.StatusCode.Equals(HttpStatusCode.TooManyRequests))
        {
            _logger.LogWarning($"Request to {requestUri} failed with status code {response.StatusCode}");

            if (requestTry >= 3)
                return default;

            await Task.Delay(2000);
            _logger.LogInformation($"Retrying request to {requestUri} (try {requestTry + 1})");
            return await SendRequest<T>(requestUri, requestTry + 1);
        }

        if (!response.IsSuccessStatusCode) return default;

        if (typeof(T) == typeof(byte[])) return (T)(object)await response.Content.ReadAsByteArrayAsync();

        var content = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(content);
    }

    private async Task<(T?, bool)> SendApiRequest<T>(ApiServer server, string requestUri, Dictionary<string, string>? headers = null)
    {
        var isServerRateLimited = await _redis.Get<bool?>(RedisKey.ApiServerRateLimited(server));

        if (isServerRateLimited is true)
        {
            _logger.LogWarning($"Server {server} is rate limited. Ignoring request.");
            return (default, true);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            var response = await _client.SendAsync(request);

            var rateLimit = string.Empty;
            var rateLimitReset = "60";

            switch (server)
            {
                case ApiServer.Observatory:
                    rateLimit = response.Headers.TryGetValues("RateLimit-Remaining", out var rateLimitHeader)
                        ? rateLimitHeader.FirstOrDefault()
                        : "60";
                    rateLimitReset = response.Headers.TryGetValues("RateLimit-Reset", out rateLimitHeader)
                        ? rateLimitHeader.FirstOrDefault()
                        : "60";
                    break;
                default:
                    _logger.LogWarning($"Server {server} rate limit headers wasn't set. Ignoring rate limit.");
                    break;
            }

            if (rateLimit is not null && int.TryParse(rateLimit, out var rateLimitInt) && rateLimitInt <= 5)
                await _redis.Set(RedisKey.ApiServerRateLimited(server),
                    true,
                    TimeSpan.FromSeconds(int.TryParse(rateLimitReset, out var rateLimitResetInt)
                        ? rateLimitResetInt
                        : 60));

            if (response.StatusCode.Equals(HttpStatusCode.TooManyRequests))
            {
                _logger.LogWarning(
                    $"Request to {server} failed with status code {response.StatusCode}. Rate limiting server for 1 minute.");

                await _redis.Set(RedisKey.ApiServerRateLimited(server), true, TimeSpan.FromMinutes(1));

                return (default, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode < HttpStatusCode.BadGateway)
                    return (default, false);

                _logger.LogWarning(
                    $"{server} returned status code {response.StatusCode}. Ignoring server for 10 minutes.");
                await _redis.Set(RedisKey.ApiServerRateLimited(server), true, TimeSpan.FromMinutes(10));

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

                    var jsonDoc = JsonDocument.Parse(content);

                    if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object && jsonDoc.RootElement.TryGetProperty("status", out var status))
                    {
                        if (status.ValueKind == JsonValueKind.Number && status.GetInt32() != 200)
                        {
                            _logger.LogError($"Failed to process request to {server} with uri {requestUri}. Status: {status}");
                            return (default, false);
                        }
                    }

                    return (JsonSerializer.Deserialize<T>(content), false);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to process request to {server} with uri {requestUri}");
            return (default, false);
        }
    }

    private string SerializeUrlQuery(string url)
    {
        var results = HttpUtility.ParseQueryString(url);
        var nonEmpty = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(results.AllKeys[0])) return url;

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