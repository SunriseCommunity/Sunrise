using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Controllers;

[Route("/user")]
[Subdomain("api")]
public class UserController : ControllerBase
{
    [HttpGet]
    [Route("{id:int}")]
    public async Task<IActionResult> GetUser(int id, [FromQuery(Name = "mode")] int? mode)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(id);

        if (user == null)
        {
            return NotFound("User not found");
        }

        if (mode == null)
        {
            return Ok(new UserResponse(user));
        }

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);

        if (isValidMode != true)
        {
            return BadRequest("Invalid mode parameter");
        }

        var stats = await database.GetUserStats(id, (GameMode)mode);

        if (stats == null)
        {
            return NotFound("User stats not found");
        }

        var data = JsonSerializer.SerializeToElement(new
        {
            user = new UserResponse(user),
            stats = new UserStatsResponse(stats)
        });

        return Ok(data);
    }

    [HttpGet]
    [Route("self")]
    public async Task<IActionResult> GetSelfUser([FromQuery(Name = "mode")] int? mode)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized("Invalid session");

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(session.User.Id);

        if (user == null)
        {
            return NotFound("User not found");
        }

        if (mode == null)
        {
            return Ok(new UserResponse(user));
        }

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);

        if (isValidMode != true)
        {
            return BadRequest("Invalid mode parameter");
        }

        var stats = await database.GetUserStats(session.User.Id, (GameMode)mode);

        if (stats == null)
        {
            return NotFound("User stats not found");
        }

        var data = JsonSerializer.SerializeToElement(new
        {
            user = new UserResponse(user),
            stats = new UserStatsResponse(stats)
        });

        return Ok(data);
    }

    [HttpGet]
    [Route("{id:int}/scores")]
    public async Task<IActionResult> GetUserScores(int id, [FromQuery(Name = "mode")] int mode)
    {
        if (mode is < 0 or > 3)
        {
            return BadRequest("Invalid mode parameter");
        }

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(id);

        if (user == null)
        {
            return NotFound("User not found");
        }

        var stats = await database.GetUserBestScores(id, (GameMode)mode);

        var scores = stats.Select(score => new ScoreResponse(score)).ToList();

        return Ok(scores);
    }

    [HttpGet]
    [Route("all")]
    public async Task<IActionResult> GetAllUsers([FromQuery(Name = "mode")] int? mode)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var users = await database.GetAllUsers();

        if (users == null)
        {
            return NotFound("Users not found");
        }

        var usersResponse = users.Select(user => new UserResponse(user)).ToList();

        if (mode == null)
        {
            return Ok(usersResponse);
        }

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);

        if (isValidMode != true)
        {
            return BadRequest("Invalid mode parameter");
        }

        var stats = await database.GetAllUserStats((GameMode)mode);

        if (stats == null)
        {
            return NotFound("Users not found");
        }

        var data = JsonSerializer.SerializeToElement(new
        {
            users = usersResponse,
            stats = stats.Select(stat => new UserStatsResponse(stat)).ToList()
        });

        return Ok(data);
    }

    [HttpPost(RequestType.AvatarUpload)]
    public async Task<IActionResult> SetAvatar()
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized("Invalid session");

        var file = Request.Form.Files[0];
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, Request.HttpContext.RequestAborted);

        var (isSet, error) = await AssetService.SetAvatar(session.User.Id, buffer);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.AvatarUpload, null, error);
            return BadRequest(error);
        }

        return new OkResult();
    }

    [HttpPost(RequestType.BannerUpload)]
    public async Task<IActionResult> SetBanner()
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized("Invalid session");

        var file = Request.Form.Files[0];
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, Request.HttpContext.RequestAborted);

        var (isSet, error) = await AssetService.SetBanner(session.User.Id, buffer);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.BannerUpload, null, error);
            return BadRequest(error);
        }

        return new OkResult();
    }
}