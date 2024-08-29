using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.API.Managers;
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
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized("Invalid session");

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(id);

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

        var stats = await database.GetUserStats(id, (GameMode)mode);

        var data = JsonSerializer.SerializeToElement(new
        {
            user,
            stats
        });

        return Ok(data);
    }

    [HttpPost(RequestType.AvatarUpload)]
    public async Task<IActionResult> SetAvatar()
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized("Invalid session");

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, Request.HttpContext.RequestAborted);

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

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, Request.HttpContext.RequestAborted);

        var (isSet, error) = await AssetService.SetBanner(session.User.Id, buffer);

        if (!isSet || error != null)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.BannerUpload, null, error);
            return BadRequest(error);
        }

        return new OkResult();
    }
}