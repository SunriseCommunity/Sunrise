using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Sunrise.WebServer.Services;

namespace Sunrise.WebServer.Controllers;

[Controller]
[Route("/web")]
public class WebController : ControllerBase
{
    private readonly ScoreService _scoreService;
    private readonly FileService _fileService;

    private const string StaticVersionResponse =
        "[{\"file_version\":\"3\",\"filename\":\"avcodec-51.dll\",\"file_hash\":\"b22bf1e4ecd4be3d909dc68ccab74eec\",\"filesize\":\"4409856\",\"timestamp\":\"2014-08-18 16:16:59\",\"patch_id\":\"1349\",\"url_full\":\"http:\\/\\/m1.ppy.sh\\/r\\/avcodec-51.dll\\/f_b22bf1e4ecd4be3d909dc68ccab74eec\",\"url_patch\":\"http:\\/\\/m1.ppy.sh\\/r\\/avcodec-51.dll\\/p_b22bf1e4ecd4be3d909dc68ccab74eec_734e450dd85c16d62c1844f10c6203c0\"}]";


    public WebController(ScoreService scoreService, FileService fileService)
    {
        _scoreService = scoreService;
        _fileService = fileService;
    }


    [HttpPost]
    [Route("osu-submit-modular-selector.php")]
    [Description("Handles score submission.")]
    public async Task<IActionResult> Submit()
    {
        try
        {
            var result = await _scoreService.SubmitScore(Request);
            return await Task.FromResult<IActionResult>(Ok(result));
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpGet]
    [Route("osu-osz2-getscores.php")]
    [Description("Handles score fetching for beatmap.")]
    public async Task<OkObjectResult> GetScores()
    {
        var result = await _scoreService.GetBeatmapScores(Request);
        return Ok(result);
    }

    [HttpPost]
    [Route("osu-error.php")]
    public IActionResult OsuError()
    {
        return Ok();
    }

    [HttpGet]
    [Route("bancho_connect.php")]
    public IActionResult BanchoConnect(string v, string u, string h, string fail = "", string fx = "", string ch = "", string retry = "")
    {
        return Ok("fi");
    }

    [HttpPost]
    [Route("osu-session.php")]
    public IActionResult OsuConnect()
    {
        return Ok();
    }

    [HttpGet]
    [Route("check-updates.php")]
    public IActionResult CheckUpdates()
    {
        return Ok(StaticVersionResponse);
    }

    [HttpGet]
    [Route("osu-getseasonal.php")]
    public IActionResult GetSeasonal()
    {
        var result = _fileService.GetSeasonalBackgrounds();
        return Ok(result);
    }
}