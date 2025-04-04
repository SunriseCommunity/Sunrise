using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Hangfire.Dashboard;
using Microsoft.Extensions.Caching.Memory;
using Sunrise.Shared.Application;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Services;

namespace Sunrise.Server;

public sealed class Middleware(
    IMemoryCache cache
) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var ip = RegionService.GetUserIpAddress(context.Request);

        var path = context.Request.Path;

        var isApiRequest = context.Request.Host.Host.StartsWith("api.");

        if (Configuration.BannedIps.Contains(ip.ToString()) && !isApiRequest)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (path.StartsWithSegments(Configuration.ApiDocumentationPath) && !isApiRequest)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (isApiRequest && Configuration.OnMaintenance)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json; charset=utf-8";

            var responseMessage = new
            {
                error = "Service is currently unavailable due to maintenance. Please try again later."
            };

            var jsonResponse = JsonSerializer.Serialize(responseMessage);
            await context.Response.WriteAsync(jsonResponse);

            return;
        }

        if (path.StartsWithSegments("/metrics") && !ip.IsFromLocalNetwork() &&
            !ip.IsFromDocker())
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // TODO: Add caching for assets
        if (context.Request.Host.Host.StartsWith("a."))
        {
            await next(context);
            return;
        }

        var limiter = GetRateLimiter(ip);
        using var lease = await limiter.AcquireAsync(1, context.RequestAborted);

        if (limiter.GetStatistics() is { } statistics)
        {
            context.Response.Headers["X-RateLimit-Limit"] = $"{Configuration.GeneralCallsPerWindow}";
            context.Response.Headers["X-RateLimit-Remaining"] = $"{statistics.CurrentAvailablePermits}";
            if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.Response.Headers.RetryAfter = $"{retryAfter.Seconds}";
        }

        if (lease.IsAcquired is false)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        await next(context);
    }

    private TokenBucketRateLimiter GetRateLimiter(IPAddress key)
    {
        return cache.GetOrCreate(key,
            entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromSeconds(Configuration.GeneralWindow);
                return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    AutoReplenishment = true,
                    TokenLimit = Configuration.GeneralCallsPerWindow,
                    TokensPerPeriod = Configuration.GeneralCallsPerWindow,
                    QueueLimit = 0,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(Configuration.GeneralWindow)
                });
            }) ?? throw new InvalidOperationException($"Failed to create rate limiter for {key}");
    }
}

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var ip = RegionService.GetUserIpAddress(context.GetHttpContext().Request);

        return ip.IsFromLocalNetwork() ||
               ip.IsFromDocker();
    }
}