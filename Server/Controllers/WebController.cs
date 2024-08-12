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

    [HttpGet("maps/{filename}")]
    public async Task<IActionResult> GetMap(string filename)
    {
        var file = await BeatmapService.GetBeatmapFile(filename);

        if (file == null)
            return NotFound("Beatmap not found");

        return new FileContentResult(file, "application/octet-stream");
    }

    [HttpPost("osu-error.php")]
    public IActionResult OsuError()
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

    [Route("/users")]
    [HttpPost]
    public async Task<IActionResult> Register()
    {
        var result = await AuthService.Register(Request);
        return result is BadRequestObjectResult ? result : Ok(result);
    }
}