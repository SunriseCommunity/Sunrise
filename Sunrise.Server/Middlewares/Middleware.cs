using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Sunrise.API.Attributes;
using Sunrise.API.Extensions;
using Sunrise.API.Objects.Keys;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Middlewares;

public sealed class Middleware(
    IMemoryCache cache,
    DatabaseService database
) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var ip = RegionService.GetUserIpAddress(context.Request);

        var path = context.Request.Path;

        var isApiRequest = context.Request.Host.Host.StartsWith("api.");
        var isAssetsRequest = context.Request.Host.Host.StartsWith("a.") || context.Request.Host.Host.StartsWith("assets.");

        if (path.StartsWithSegments(Configuration.ApiDocumentationPath) && !isApiRequest)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var isIpFormLocalNetwork = ip.IsFromLocalNetwork() || ip.IsFromDocker();

        if (path.StartsWithSegments("/metrics") && !isIpFormLocalNetwork)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (Configuration.BannedIps.Contains(ip.ToString()) && !isApiRequest)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (isAssetsRequest)
        {
            await next(context);
            return;
        }

        var isBannedIp = await ShouldStopBannedIps(context);
        if (isBannedIp)
            return;

        var isRateLimited = await FillRateLimitHeadersAndGetIsRateLimited(context);
        if (isRateLimited)
            return;

        await SetCurrentUserFromRequestIfPossible(context);

        var isShouldStopApiRequest = await ShouldStopUnauthorizedApiRequestIfOnMaintenance(context);
        if (isShouldStopApiRequest)
            return;

        var isAuthorizeRequestDoesntHasUser = await ShouldStopIfHasAuthorizationButDontHaveUser(context);
        if (isAuthorizeRequestDoesntHasUser)
            return;

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
                    QueueLimit = Configuration.QueueLimit,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(Configuration.GeneralWindow)
                });
            }) ?? throw new InvalidOperationException($"Failed to create rate limiter for {key}");
    }

    private async Task<bool> FillRateLimitHeadersAndGetIsRateLimited(HttpContext context)
    {
        var ip = RegionService.GetUserIpAddress(context.Request);

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
            return true;
        }

        return false;
    }

    private async Task<bool> ShouldStopIfHasAuthorizationButDontHaveUser(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var hasAuthorize = endpoint?.Metadata?.GetMetadata<AuthorizeAttribute>() != null;

        if (hasAuthorize && context.Items["CurrentUser"] == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json; charset=utf-8";

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Detail = ApiErrorResponse.Detail.AuthorizationFailed
            };

            var jsonResponse = JsonSerializer.Serialize(problemDetails);
            await context.Response.WriteAsync(jsonResponse);
            return true;
        }

        return false;
    }

    private async Task SetCurrentUserFromRequestIfPossible(HttpContext context)
    {
        var userClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        var hashClaim = context.User.FindFirst(ClaimTypes.Hash);

        if (userClaim != null && int.TryParse(userClaim.Value, out var userId) && hashClaim != null)
        {
            var user = await database.Users.GetValidUser(userId);

            if (user == null)
                return;

            var isValidPasshash = hashClaim.Value == $"{user.Id}{user.Passhash}".ToHash();

            if (isValidPasshash)
            {
                context.Items["CurrentUser"] = user;

                if (user.AccountStatus == UserAccountStatus.Disabled)
                {
                    database.Users.Moderation.EnableUser(user.Id).Wait();
                    // TODO: Send message from bot about account being enabled
                }
            }
        }
    }

    private async Task<bool> ShouldStopBannedIps(HttpContext context)
    {
        var ip = RegionService.GetUserIpAddress(context.Request);

        if (Configuration.BannedIps.Contains(ip.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json; charset=utf-8";

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Detail = ApiErrorResponse.Detail.YouHaveBeenBanned
            };

            var jsonResponse = JsonSerializer.Serialize(problemDetails);
            await context.Response.WriteAsync(jsonResponse);
            return true;
        }

        return false;
    }


    private async Task<bool> ShouldStopUnauthorizedApiRequestIfOnMaintenance(HttpContext context)
    {
        var isApiRequest = context.Request.Host.Host.StartsWith("api.");

        var endpoint = context.GetEndpoint();
        var hasIgnoreMaintenance = endpoint?.Metadata.GetMetadata<IgnoreMaintenanceAttribute>() != null;

        if (hasIgnoreMaintenance)
        {
            return false;
        }

        var user = context.GetCurrentUser();
        var ignoreMaintenanceMode = user != null && user.Privilege.HasFlag(UserPrivilege.Admin);

        if (ignoreMaintenanceMode)
        {
            return false;
        }

        if (isApiRequest && Configuration.OnMaintenance)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json; charset=utf-8";

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "Service is currently unavailable due to maintenance. Please try again later."
            };

            var jsonResponse = JsonSerializer.Serialize(problemDetails);
            await context.Response.WriteAsync(jsonResponse);
            return true;
        }

        return false;
    }
}