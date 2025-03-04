using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    [ResponseCache(VaryByHeader = "User-Agent", Duration = 60)]
    [EndpointDescription("Check server status")]
    [ProducesResponseType(typeof(StatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus([FromQuery(Name = "detailed")] bool detailed = false)
    {
        var usersOnline = sessions.GetSessions().Count;
        var totalUsers = await database.Users.CountUsers();

        if (detailed)
        {
            var totalScores = await database.Scores.CountScores();
            return Ok(new StatusResponse(usersOnline, totalUsers, totalScores));
        }

        return Ok(new StatusResponse(usersOnline, totalUsers));
    }
}