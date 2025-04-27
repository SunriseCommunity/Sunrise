using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable;
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
        using var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<HttpClientService>();
        _redis = redis;

        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Sunrise");
    }

    public async Task<Result<T, ErrorMessage>> PostRequestWithBody<T>(BaseSession session, ApiType type, object body, Dictionary<string, string>? headers = null)
    {
        if (session.IsRateLimited())
        {
            if (session is Session gameSession)
            {
                gameSession.SendRateLimitWarning();
            }

            _logger.LogWarning($"User {session.UserId} got rate limited. Ignoring request.");

            return Result.Failure<T, ErrorMessage>(new ErrorMessage
            {
                Message = "You're sending requests faster than we can handle! Please wait a moment before trying again.",
                Status = HttpStatusCode.TooManyRequests
            });
        }

        headers ??= new Dictionary<string, string>();

        var apis = Configuration.ExternalApis.Where(x => x.Type == type && x.ShouldHaveBody).OrderBy(x => x.Priority).ToList();

        if (apis is { Count: 0 } or null)
        {
            _logger.LogWarning($"No API servers found for {type}.");
            return Result.Failure<T, ErrorMessage>(new ErrorMessage
            {
                Message = $"No API servers found for {type}.",
                Status = HttpStatusCode.BadRequest
            });
        }

        foreach (var api in apis)
        {
            SunriseMetrics.ExternalApiRequestsCounterInc(type, api.Server, session);

            if (api.Server == ApiServer.Observatory)
            {
                if (!string.IsNullOrEmpty(Configuration.ObservatoryApiKey))
                    headers.Add("Authorization", $"{Configuration.ObservatoryApiKey}");
            }

            var responseResult = await SendApiRequest<T>(api.Server, api.Url, headers, body);

            if (responseResult.IsSuccess) return responseResult;

            if (!responseResult.IsFailure)
                continue;

            if (responseResult.Error.Status == HttpStatusCode.TooManyRequests) continue;

            return responseResult;
        }

        _logger.LogWarning(
            $"Failed to get response from any API server for {type}.");

        return Result.Failure<T, ErrorMessage>(new ErrorMessage
        {
            Message = $"Failed to get response from any API server for {type}.",
            Status = HttpStatusCode.BadRequest
        });
    }

    public async Task<Result<T, ErrorMessage>> SendRequest<T>(BaseSession session, ApiType type, object?[] args, Dictionary<string, string>? headers = null, CancellationToken ct = default)
    {
        if (session.IsRateLimited())
        {
            if (session is Session gameSession)
            {
                gameSession.SendRateLimitWarning();
            }

            _logger.LogWarning($"User {session.UserId} got rate limited. Ignoring request.");

            return Result.Failure<T, ErrorMessage>(new ErrorMessage
            {
                Message = "You're sending requests faster than we can handle! Please wait a moment before trying again.",
                Status = HttpStatusCode.TooManyRequests
            });
        }

        headers ??= new Dictionary<string, string>();

        var apis = Configuration.ExternalApis.Where(x => x.Type == type && !x.ShouldHaveBody).OrderBy(x => x.Priority).ToList();

        if (apis is { Count: 0 } or null)
        {
            _logger.LogWarning($"No API servers found for {type}.");
            return Result.Failure<T, ErrorMessage>(new ErrorMessage
            {
                Message = $"No API servers found for {type}.",
                Status = HttpStatusCode.BadRequest
            });
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

            var responseResult = await SendApiRequest<T>(api.Server, requestUri, headers, ct: ct);

            if (responseResult.IsSuccess) return responseResult;

            if (!responseResult.IsFailure)
                continue;

            if (responseResult.Error.Status == HttpStatusCode.TooManyRequests) continue;

            return responseResult;
        }

        _logger.LogWarning(
            $"Failed to get response from any API server for {type} with args {string.Join(", ", args)}.");

        return Result.Failure<T, ErrorMessage>(new ErrorMessage
        {
            Message = $"Failed to get response from any API server for {type} with args {string.Join(", ", args)}.",
            Status = HttpStatusCode.BadRequest
        });
    }

    private async Task<Result<T, ErrorMessage>> SendApiRequest<T>(ApiServer server, string requestUri, Dictionary<string, string>? headers = null, object? body = null, CancellationToken ct = default)
    {
        var isServerRateLimited = await _redis.Get<bool?>(RedisKey.ApiServerRateLimited(server));

        if (isServerRateLimited is true)
        {
            _logger.LogWarning($"Server {server} is rate limited. Ignoring request.");

            return Result.Failure<T, ErrorMessage>(new ErrorMessage
            {
                Message = $"Server {server} is rate limited. Ignoring request.",
                Status = HttpStatusCode.TooManyRequests
            });
        }

        try
        {
            using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, requestUri);

            if (body != null)
            {
                var json = JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            var response = await _client.SendAsync(request, ct);

            var rateLimit = string.Empty;
            var rateLimitReset = "60";

            switch (server)
            {
                case ApiServer.Observatory:
                    rateLimit = response.Headers.TryGetValues("RateLimit-Remaining", out var rateLimitHeader)
                        ? rateLimitHeader.FirstOrDefault()
                        : "300"; // If you use dev token, observatory will not try to stop any of your requests, but you still need to behave yourself.
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

                return Result.Failure<T, ErrorMessage>(new ErrorMessage
                {
                    Message = $"Request to {server} failed with status code {response.StatusCode}. Rate limiting server for 1 minute.",
                    Status = HttpStatusCode.TooManyRequests
                });
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode < HttpStatusCode.BadGateway)
                {
                    return Result.Failure<T, ErrorMessage>(new ErrorMessage
                    {
                        Message = $"Request to {server} failed with status code {response.StatusCode}.",
                        Status = response.StatusCode
                    });
                }
            }

            switch (typeof(T))
            {
                case not null when typeof(T) == typeof(byte[]):
                    return (T)(object)await response.Content.ReadAsByteArrayAsync(ct);
                case not null when typeof(T) == typeof(string):
                    return (T)(object)await response.Content.ReadAsStringAsync(ct);
                default:
                    var content = await response.Content.ReadAsStringAsync(ct);

                    var jsonDoc = JsonDocument.Parse(content);

                    if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object && jsonDoc.RootElement.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Number)
                    {
                        var statusCode = status.GetInt32();

                        if (statusCode == 404)
                        {
                            return Result.Failure<T, ErrorMessage>(JsonSerializer.Deserialize<ErrorMessage>(content) ?? new ErrorMessage
                            {
                                Message = "Exception occured while deserializing error message",
                                Status = HttpStatusCode.BadRequest
                            });
                        }

                        if (statusCode != 200)
                        {
                            _logger.LogError($"Failed to process request to {server} with uri {requestUri}. Status: {status}");
                            return Result.Failure<T, ErrorMessage>(new ErrorMessage
                            {
                                Message = $"Failed to process request to {server} with uri {requestUri}. Status: {status}",
                                Status = HttpStatusCode.BadRequest
                            });
                        }
                    }

                    return JsonSerializer.Deserialize<T>(content) ?? Result.Failure<T, ErrorMessage>(new ErrorMessage
                    {
                        Message = "Exception occured while deserializing data",
                        Status = HttpStatusCode.BadRequest
                    });
            }
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<T, ErrorMessage>(new ErrorMessage
            {
                Message = "Operation was cancelled.",
                Status = HttpStatusCode.BadRequest
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to process request to {server} with uri {requestUri}");
            return Result.Failure<T, ErrorMessage>(new ErrorMessage
            {
                Message = $"Failed to process request to {server} with uri {requestUri}",
                Status = HttpStatusCode.BadRequest
            });
        }
    }

    private string SerializeUrlQuery(string url)
    {
        var uriBuilder = new UriBuilder(url);
        var queryParameters = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach (var key in queryParameters.AllKeys)
        {
            if (string.IsNullOrEmpty(queryParameters[key]))
            {
                queryParameters.Remove(key);
            }
        }

        uriBuilder.Query = queryParameters.ToString();
        return uriBuilder.ToString();
    }
}