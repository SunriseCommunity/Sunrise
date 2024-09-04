using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[Route("/web")]
[Subdomain("osu")]
public class WebController : ControllerBase
{
    [HttpPost(RequestType.OsuScreenshot)]
    public async Task<IActionResult> OsuScreenshot(
        [FromForm(Name = "u")] string username,
        [FromForm(Name = "p")] string passhash,
        [FromForm(Name = "ss")] IFormFile screenshot)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        if (await AssetService.SaveScreenshot(session, screenshot, HttpContext.RequestAborted) is var (resultUrl, error) && (error != null || resultUrl == null))
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuScreenshot, session, error);
            return BadRequest(error);
        }

        return Ok(resultUrl);
    }

    [HttpGet(RequestType.OsuGetFriends)]
    public async Task<IActionResult> OsuGetFriends(
        [FromQuery(Name = "u")] string username,
        [FromQuery(Name = "h")] string passhash)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        var friends = await BanchoService.GetFriends(session.User.Username);
        if (friends == null)
            return BadRequest("error: no");

        return Ok(friends);
    }

    [HttpPost(RequestType.OsuError)]
    public IActionResult OsuError()
    {
        return Ok();
    }

    [HttpGet(RequestType.LastFm)]
    public IActionResult LastFm(
        [FromQuery(Name = "us")] string username,
        [FromQuery(Name = "ha")] string passhash,
        [FromQuery(Name = "b")] string query)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        if (query[0] != 'a')
            return Ok("-3");

        var flags = (LastFmFlags)int.Parse(query[1..]);

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        if ((flags & (LastFmFlags.HqAssembly | LastFmFlags.HqFile)) != 0)
        {
            _ = database.RestrictPlayer(session.User.Id, -1, "hq!osu found running");
            return Ok("-3");
        }

        if ((flags & LastFmFlags.RegistryEdits) != 0)
        {
            _ = database.RestrictPlayer(session.User.Id, -1, "Osu multi account registry edits found");
            return Ok("-3");
        }

        return Ok();
    }

    [HttpGet(RequestType.OsuMarkAsRead)]
    public IActionResult OsuMarkAsRead()
    {
        return Ok();
    }

    [HttpGet(RequestType.BanchoConnect)]
    public IActionResult BanchoConnect()
    {
        return Ok();
    }

    [HttpPost(RequestType.OsuSession)]
    public IActionResult OsuConnect()
    {
        return Ok();
    }

    [HttpGet(RequestType.CheckUpdates)]
    public IActionResult CheckUpdates()
    {
        var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        return Redirect($"https://osu.ppy.sh/web/{RequestType.CheckUpdates}{queryString}");
    }

    [HttpGet(RequestType.OsuGetSeasonalBackground)]
    public IActionResult GetSeasonal()
    {
        var result = AssetService.GetSeasonalBackgrounds();
        return Ok(result);
    }

    [HttpPost(RequestType.PostRegister)]
    public async Task<IActionResult> Register()
    {
        var result = await AuthService.Register(Request);
        return result is BadRequestObjectResult ? result : Ok(result);
    }

    [HttpGet("/wiki/en/Do_you_really_want_to_ask_peppy")]
    public async Task<IActionResult> AskPeppy()
    {
        var image = await AssetService.GetPeppyImage();
        if (image == null)
            return NotFound();

        return new FileContentResult(image, "image/jpeg");
    }

    [Obsolete("Temporary while I work on the website")]
    [Route("/beatmapsets/{*path}")]
    [HttpGet]
    public IActionResult RedirectToMirrorSets(string path)
    {
        return Redirect($"https://osu.direct/beatmapsets/{path}");
    }

    [Obsolete("Temporary while I work on the website")]
    [Route("/beatmaps/{*path}")]
    [HttpGet]
    public IActionResult RedirectToMirrorMaps(string path)
    {
        return Redirect($"https://osu.direct/beatmaps/{path}");
    }
}