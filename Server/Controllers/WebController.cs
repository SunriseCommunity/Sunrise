using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Helpers;
using Sunrise.Server.Services;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[ApiController]
[Route("/web")]
[SubdomainAttribute("osu")]
public class WebController(ScoreService scoreService, FileService fileService, ServicesProvider services) : ControllerBase
{
    // TODO: Enhance this response
    private const string StaticVersionResponse =
        "[{\"file_version\":\"3\",\"filename\":\"avcodec-51.dll\",\"file_hash\":\"b22bf1e4ecd4be3d909dc68ccab74eec\",\"filesize\":\"4409856\",\"timestamp\":\"2014-08-18 16:16:59\",\"patch_id\":\"1349\",\"url_full\":\"http:\\/\\/m1.ppy.sh\\/r\\/avcodec-51.dll\\/f_b22bf1e4ecd4be3d909dc68ccab74eec\",\"url_patch\":\"http:\\/\\/m1.ppy.sh\\/r\\/avcodec-51.dll\\/p_b22bf1e4ecd4be3d909dc68ccab74eec_734e450dd85c16d62c1844f10c6203c0\"}]";

    private readonly AuthorizationHelper _authorizationHelper = new(services);

    [HttpPost("osu-submit-modular-selector.php")]
    public async Task<IActionResult> Submit()
    {
        if (await _authorizationHelper.IsAuthorized(Request) == false)
            return BadRequest("Invalid request: Unauthorized");

        var result = await scoreService.SubmitScore(Request);
        return await Task.FromResult<IActionResult>(Ok(result));
    }

    [HttpGet("osu-getreplay.php")]
    public async Task<IActionResult> GetReplay()
    {
        var scoreId = Request.Query["c"];

        if (string.IsNullOrEmpty(scoreId))
            return BadRequest("Invalid request: Missing parameters");

        var result = await fileService.GetOsuReplayBytes(int.Parse(scoreId!));

        return new FileContentResult(result, "application/octet-stream");
    }

    [HttpGet("osu-osz2-getscores.php")]
    public async Task<IActionResult> GetScores()
    {
        if (await _authorizationHelper.IsAuthorized(Request) == false)
            return BadRequest("Invalid request: Unauthorized");

        var result = await scoreService.GetBeatmapScores(Request);
        return Ok(result);
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
        var result = fileService.GetSeasonalBackgrounds();
        return Ok(result);
    }
}