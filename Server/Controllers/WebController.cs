using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Services;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[ApiController]
[Route("/web")]
[Subdomain("osu")]
public class WebController : ControllerBase
{
    [HttpPost("osu-submit-modular-selector.php")]
    public async Task<IActionResult> Submit()
    {
        var result = await ScoreService.SubmitScore(Request);
        return await Task.FromResult<IActionResult>(Ok(result));
    }

    [HttpGet("osu-getreplay.php")]
    public async Task<IActionResult> GetReplay()
    {
        var scoreId = Request.Query["c"];

        if (string.IsNullOrEmpty(scoreId))
            return BadRequest("Invalid request: Missing parameters");

        var result = await FileService.GetOsuReplayBytes(int.Parse(scoreId!));

        return new FileContentResult(result, "application/octet-stream");
    }

    [HttpGet("osu-osz2-getscores.php")]
    public async Task<IActionResult> GetScores()
    {
        if (await AuthorizationHelper.IsAuthorized(Request) == false)
            return BadRequest("Invalid request: Unauthorized");

        var result = await ScoreService.GetBeatmapScores(Request);
        return Ok(result);
    }

    [HttpGet("osu-search.php")]
    public async Task<IActionResult> Search()
    {
        if (await AuthorizationHelper.IsAuthorized(Request) == false)
            return BadRequest("Invalid request: Unauthorized");

        var result = await BeatmapService.SearchBeatmapSet(Request);

        if (result == null)
            return BadRequest("Invalid request: Invalid request");

        return Ok(result);
    }

    [HttpGet("osu-search-set.php")]
    public async Task<IActionResult> SearchBySetId()
    {
        if (await AuthorizationHelper.IsAuthorized(Request) == false)
            return BadRequest("Invalid request: Unauthorized");

        var result = await BeatmapService.SearchBeatmapSetByIds(Request);

        if (result == null)
            return BadRequest("Invalid request: Invalid request");

        return Ok(result);
    }

    [HttpGet("osu-getfriends.php")]
    public async Task<IActionResult> OsuGetFriends()
    {
        if (await AuthorizationHelper.IsAuthorized(Request) == false)
            return BadRequest("Invalid request: Unauthorized");

        var friends = await BanchoService.GetFriends(Request.Query["u"]!);

        if (friends == null)
            return BadRequest("Invalid request: Invalid request");

        return Ok(friends);
    }

    [HttpPost("osu-error.php")]
    public IActionResult OsuError()
    {
        return Ok();
    }

    [HttpGet("lastfm.php")]
    public IActionResult LastFm()
    {
        return Ok();
    }

    [HttpGet("osu-markasread.php")]
    public IActionResult OsuMarkAsRead()
    {
        return Ok();
    }

    [HttpGet("bancho_connect.php")]
    public IActionResult BanchoConnect()
    {
        return Ok();
    }

    [HttpPost("osu-session.php")]
    public IActionResult OsuConnect()
    {
        return Ok();
    }

    [HttpPost("osu-screenshot.php")]
    public async Task<IActionResult> OsuScreenshot()
    {
        var username = Request.Form["u"];
        var passhash = Request.Form["p"];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passhash))
        {
            return BadRequest("Invalid request: Missing parameters");
        }

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var user = await database.GetUser(username: username, passhash: passhash);

        if (user == null)
        {
            return BadRequest("Invalid request: User not found");
        }

        var resultUrl = await FileService.SaveScreenshot(Request, user);

        return Ok(resultUrl);
    }

    [HttpGet("check-updates.php")]
    public IActionResult CheckUpdates()
    {
        var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        var redirectUrl = $"https://osu.ppy.sh/web/check-updates.php{queryString}";
        return Redirect(redirectUrl);
    }

    [HttpGet("osu-getseasonal.php")]
    public IActionResult GetSeasonal()
    {
        var result = FileService.GetSeasonalBackgrounds();
        return Ok(result);
    }

    [Route("/d/{id}")]
    [HttpGet]
    public IActionResult DownloadBeatmapset(string id)
    {
        return Redirect($"https://osu.direct/api/d/{id}");
    }

    [Route("/users")]
    [HttpPost]
    public async Task<IActionResult> Register()
    {
        var result = await AuthService.Register(Request);
        return result is BadRequestObjectResult ? result : Ok(result);
    }

    [Obsolete("Temporary while I work on the website")]
    [Route("/beatmapsets/{*path}")]
    [HttpGet]
    public IActionResult RedirectToMirrorSets(string path)
    {
        Console.WriteLine(path);
        return Redirect($"https://osu.direct/beatmapsets/{path}");
    }

    [Obsolete("Temporary while I work on the website")]
    [Route("/beatmaps/{*path}")]
    [HttpGet]
    public IActionResult RedirectToMirrorMaps(string path)
    {
        Console.WriteLine(path);
        return Redirect($"https://osu.direct/beatmaps/{path}");
    }
}