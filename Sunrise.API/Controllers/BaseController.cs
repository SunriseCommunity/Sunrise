using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Sunrise.API.Managers;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using RateLimiter = System.Threading.RateLimiting.RateLimiter;

namespace Sunrise.API.Controllers;

[ApiController]
[Subdomain("api")]
public class BaseController(IMemoryCache cache) : ControllerBase
{
    [HttpGet]
    [Route("/ping")]
    public IActionResult Index()
    {
        return Ok("Sunrise API");
    }

    [HttpGet]
    [Route("/limits")]
    public async Task<IActionResult> GetLimits()
    {
        var key = RegionService.GetUserIpAddress(Request);
        var limiter = cache.Get(key) as RateLimiter;
        var statistics = limiter?.GetStatistics();

        var session = await Request.GetSessionFromRequest();

        return Ok(new LimitsResponse(statistics?.CurrentAvailablePermits, session?.GetRemainingCalls()));
    }

    [HttpGet]
    [Route("/status")]
    [ResponseCache(VaryByHeader = "User-Agent", Duration = 60)]
    public async Task<IActionResult> GetStatus([FromQuery(Name = "detailed")] bool detailed = false)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var usersOnline = sessions.GetSessions().Count;
        var totalUsers = await database.UserService.GetTotalUsers();

        if (detailed)
        {
            var totalScores = await database.ScoreService.GetTotalScores();
            return Ok(new StatusResponse(usersOnline, totalUsers, totalScores));
        }

        return Ok(new StatusResponse(usersOnline, totalUsers));
    }
}