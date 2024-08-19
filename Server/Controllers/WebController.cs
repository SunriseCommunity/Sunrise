using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Services;

namespace Sunrise.Server.Controllers;

[ApiController]
[Route("/web")]
[Subdomain("osu")]
public class WebController : ControllerBase
{
    private const string StaticVersionResponse =
        "[{\"file_version\":\"3\",\"filename\":\"avcodec-51.dll\",\"file_hash\":\"b22bf1e4ecd4be3d909dc68ccab74eec\",\"filesize\":\"4409856\",\"timestamp\":\"2014-08-18 16:16:59\",\"patch_id\":\"1349\",\"url_full\":\"http:\\/\\/m1.ppy.sh\\/r\\/avcodec-51.dll\\/f_b22bf1e4ecd4be3d909dc68ccab74eec\",\"url_patch\":\"http:\\/\\/m1.ppy.sh\\/r\\/avcodec-51.dll\\/p_b22bf1e4ecd4be3d909dc68ccab74eec_734e450dd85c16d62c1844f10c6203c0\"}]";

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
        var result = await BeatmapService.SearchBeatmapSet(Request);
        return result;
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

    [HttpGet("check-updates.php")]
    public IActionResult CheckUpdates()
    {
        return Ok(StaticVersionResponse);
    }

    [HttpGet("osu-getseasonal.php")]
    public IActionResult GetSeasonal()
    {
        var result = FileService.GetSeasonalBackgrounds();
        return Ok(result);
    }

    [Route("/d/{id}")]
    [HttpGet]
    public IActionResult DownloadBeatmapset(int id)
    {
        return Redirect($"https://catboy.best/d/{id}");
    }

    [Route("/users")]
    [HttpPost]
    public async Task<IActionResult> Register()
    {
        var result = await AuthService.Register(Request);
        return result is BadRequestObjectResult ? result : Ok(result);
    }
}