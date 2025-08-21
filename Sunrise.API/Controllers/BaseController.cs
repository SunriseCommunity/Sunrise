using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sunrise.API.Attributes;
using Sunrise.API.Extensions;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using RateLimiter = System.Threading.RateLimiting.RateLimiter;

namespace Sunrise.API.Controllers;

[ApiController]
[Subdomain("api")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status400BadRequest)]
public class BaseController(IMemoryCache cache, DatabaseService database, SessionRepository sessions) : ControllerBase
{
    [HttpGet]
    [IgnoreMaintenance]
    [Route("/ping")]
    [EndpointDescription("Basic ping endpoint")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public IActionResult Index()
    {
        return Ok("Sunrise API");
    }

    [HttpGet]
    [IgnoreMaintenance]
    [Route("/limits")]
    [EndpointDescription("Check current API limits")]
    [ProducesResponseType(typeof(LimitsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLimits(CancellationToken ct = default)
    {
        var key = RegionService.GetUserIpAddress(Request);
        var limiter = cache.Get(key) as RateLimiter;
        var statistics = limiter?.GetStatistics();

        var session = HttpContext.GetCurrentSession();

        return Ok(new LimitsResponse(statistics?.CurrentAvailablePermits, session.GetRemainingCalls()));
    }

    [HttpGet]
    [IgnoreMaintenance]
    [Route("/status")]
    [EndpointDescription("Check server status")]
    [ProducesResponseType(typeof(StatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus([FromQuery(Name = "detailed")] bool detailed = false, [FromQuery(Name = "includeRecentUsers")] bool includeRecentUsers = false, CancellationToken ct = default)
    {
        var usersOnline = sessions.GetSessions().Count;
        var totalUsers = await database.Users.CountUsers(ct);

        long? totalScores = null;
        long? totalRestrictions = null;

        if (detailed)
        {
            totalScores = await database.Scores.CountScores(ct);
            totalRestrictions = await database.Users.CountRestrictedUsers(ct);
        }

        if (includeRecentUsers)
        {
            var usersOnlineData = await database.DbContext.Users
                .Where(u => sessions.GetSessions().Select(s => s.UserId).Contains(u.Id))
                .IncludeUserThumbnails()
                .OrderBy(u => u.LastOnlineTime)
                .UseQueryOptions(new QueryOptions(true, new Pagination(1, 3)))
                .ToListAsync(cancellationToken: ct);

            var usersRegisteredData = await database.DbContext.Users
                .IncludeUserThumbnails()
                .OrderByDescending(u => u.Id)
                .UseQueryOptions(new QueryOptions(true, new Pagination(1, 3)))
                .ToListAsync(cancellationToken: ct);

            return Ok(new StatusResponse(usersOnline,
                totalUsers,
                totalScores,
                totalRestrictions,
                usersOnlineData.Select(u => new UserResponse(sessions, u)).ToList(),
                usersRegisteredData.Select(u => new UserResponse(sessions, u)).ToList()));
        }

        return Ok(new StatusResponse(usersOnline, totalUsers, totalScores, totalRestrictions));
    }
}