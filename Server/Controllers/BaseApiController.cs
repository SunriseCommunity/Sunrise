using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.Services;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[ApiController]
[Subdomain("api")]
public class BaseApiController(BaseApiService baseApiService, ServicesProvider services) : ControllerBase
{
    [HttpGet]
    [Route("/ping")]
    public IActionResult Index()
    {
        return Ok("Sunrise API");
    }

    [HttpGet]
    [Route("user/{id:int}")]
    public async Task<IActionResult> GetUser(int id, [FromQuery(Name = "mode")] int? mode)
    {
        var user = await services.Database.GetUser(id);

        if (user == null)
        {
            return NotFound("User not found");
        }

        if (mode == null)
        {
            return Ok(user);
        }

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);

        if (isValidMode != true)
        {
            return BadRequest("Invalid mode parameter");
        }

        var stats = await services.Database.GetUserStats(id, (GameMode)mode);

        var data = JsonSerializer.SerializeToElement(new
        {
            user,
            stats
        });

        return Ok(data);
    }

    [HttpGet]
    [Route("beatmap/pp/{id:int}")]
    public async Task<IActionResult> GetPpCalc(int id, [FromQuery(Name = "mode")] int mode)
    {
        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);

        if (isValidMode != true)
        {
            return BadRequest("Invalid mode parameter");
        }

        var pp = await new Calculators(services).CalculatePerformancePoints(id, mode);
        var data = JsonSerializer.SerializeToElement(new
        {
            acc100 = pp.Item1,
            acc99 = pp.Item2,
            acc95 = pp.Item3
        });

        return Ok(data);
    }

    [HttpGet]
    [Route("score/{id:int}")]
    public async Task<IActionResult> GetScore(int id)
    {
        var score = await services.Database.GetScore(id);

        if (score == null)
        {
            return NotFound("Score not found");
        }

        return Ok(score);
    }

    [HttpGet]
    [Route("avatar/{id:int}")]
    public async Task<IActionResult> GetAvatar(int id)
    {
        var result = await services.Database.GetAvatar(id);
        return new FileContentResult(result, "image/png");
    }

    [HttpPost]
    [Route("avatar/upload/{id:int}")]
    public async Task<IActionResult> SetAvatar(int id)
    {
        await baseApiService.SetAvatar(id, Request);
        return new OkResult();
    }

    [HttpGet]
    [Route("banner/{id:int}")]
    public async Task<IActionResult> GetBanner(int id)
    {
        var file = await services.Database.GetBanner(id);
        return new FileContentResult(file, "image/png");
    }

    [HttpPost]
    [Route("banner/upload/{id:int}")]
    public async Task<IActionResult> SetBanner(int id)
    {
        await baseApiService.SetBanner(id, Request);
        return new OkResult();
    }
}