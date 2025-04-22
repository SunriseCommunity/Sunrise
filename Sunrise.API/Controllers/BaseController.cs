using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sunrise.API.Managers;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using RateLimiter = System.Threading.RateLimiting.RateLimiter;

namespace Sunrise.API.Controllers;

[ApiController]
[Subdomain("api")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
public class BaseController(IMemoryCache cache, SessionManager sessionManager, DatabaseService database, SessionRepository sessions) : ControllerBase
{
    [HttpGet]
    [Route("/ping")]
    [EndpointDescription("Basic ping endpoint")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public IActionResult Index()
    {
        return Ok("Sunrise API");
    }

    [HttpGet]
    [Route("/limits")]
    [EndpointDescription("Check current API limits")]
    [ProducesResponseType(typeof(LimitsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLimits()
    {
        var key = RegionService.GetUserIpAddress(Request);
        var limiter = cache.Get(key) as RateLimiter;
        var statistics = limiter?.GetStatistics();

        var session = await sessionManager.GetSessionFromRequest(Request);

        return Ok(new LimitsResponse(statistics?.CurrentAvailablePermits, session?.GetRemainingCalls()));
    }

    [HttpGet]
    [Route("/status")]
    [EndpointDescription("Check server status")]
    [ProducesResponseType(typeof(StatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus([FromQuery(Name = "detailed")] bool detailed = false, [FromQuery(Name = "includeRecentUsers")] bool includeRecentUsers = false)
    {
        var usersOnline = sessions.GetSessions().Count;
        var totalUsers = await database.Users.CountUsers();

        long? totalScores = null;
        long? totalRestrictions = null;

        if (detailed)
        {
            totalScores = await database.Scores.CountScores();
            totalRestrictions = await database.Users.CountRestrictedUsers();
        }

        if (includeRecentUsers)
        {
            var usersOnlineData = await database.DbContext.Users
                .Where(u => sessions.GetSessions().Select(s => s.UserId).Contains(u.Id))
                .OrderBy(u => u.LastOnlineTime)
                .Take(3)
                .ToListAsync();

            var usersRegisteredData = await database.DbContext.Users.OrderByDescending(u => u.Id)
                .Take(3)
                .ToListAsync();

            return Ok(new StatusResponse(usersOnline,
                totalUsers,
                totalScores,
                totalRestrictions,
                usersOnlineData.Select(u => new UserResponse(database, sessions, u)).ToList(),
                usersRegisteredData.Select(u => new UserResponse(database, sessions, u)).ToList()));
        }

        return Ok(new StatusResponse(usersOnline, totalUsers, totalScores, totalRestrictions));
    }
}