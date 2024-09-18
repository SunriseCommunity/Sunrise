using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Repositories;
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
        User user;
        var userStatus = "Offline";

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var userSession = sessions.GetSession(userId: id);

        if (userSession != null)
        {
            user = userSession.User;
            userStatus = userSession.Attributes.Status.ToText();
        }
        else
        {

            var userDb = await database.GetUser(id);

            if (userDb == null)
                return NotFound("User not found");

            user = userDb;
        }

        if (mode == null)
        {
            return Ok(new UserResponse(user, userStatus));
        }

        var isValidMode = Enum.IsDefined(typeof(GameMode), (byte)mode);

        if (isValidMode != true)
        {
            return BadRequest(new ErrorResponse("Invalid mode parameter"));
        }

        var stats = await database.GetUserStats(id, (GameMode)mode);

        if (stats == null)
        {
            return NotFound("User stats not found");
        }

        var globalRank = await database.GetUserRank(id, (GameMode)mode);

        var data = JsonSerializer.SerializeToElement(new
        {
            user = new UserResponse(user, userStatus),
            stats = new UserStatsResponse(stats, (int)globalRank)
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

        return await GetUser(session.User.Id, mode);
    }

    [HttpGet]
    [Obsolete("Calculations for graph is impossible. Should just create snapshots each day with cron operation")]
    [Route("{id:int}/scores")]
    public async Task<IActionResult> GetUserScores(int id, [FromQuery(Name = "mode")] int mode)
    {
        if (mode is < 0 or > 3)
        {
            return BadRequest(new ErrorResponse("Invalid mode parameter"));
        }

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(id);

        if (user == null)
        {
            return NotFound("User not found");
        }

        var scores = await database.GetUserBestScores(id, (GameMode)mode);

        var top100Scores = scores.Take(100).Select(score => new ScoreResponse(score)).ToList();

        return Ok(new ScoresResponse(top100Scores, scores.Count));
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
            return BadRequest(new ErrorResponse("Invalid mode parameter"));
        }

        var stats = await database.GetAllUserStats((GameMode)mode);

        if (stats == null)
        {
            return NotFound("Users not found");
        }

        var data = JsonSerializer.SerializeToElement(new
        {
            users = usersResponse,
            stats = stats.Select(async stat =>
            {
                var globalRank = await database.GetUserRank(stat.UserId, (GameMode)mode);
                return new UserStatsResponse(stat, (int)globalRank);
            }).ToList()
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
            return BadRequest(new ErrorResponse(error ?? "Failed to set avatar"));
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
            return BadRequest(new ErrorResponse(error ?? "Failed to set banner"));
        }

        return new OkResult();
    }
}