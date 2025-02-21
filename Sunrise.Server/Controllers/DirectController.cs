using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Services;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Controllers;

[Route("/web")]
[Subdomain("osu")]
public class DirectController(BeatmapService beatmapService) : ControllerBase
{
    [HttpGet(RequestType.OsuSearch)]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "u")] string username,
        [FromQuery(Name = "h")] string passhash,
        [FromQuery(Name = "p")] int page,
        [FromQuery(Name = "q")] string query,
        [FromQuery(Name = "m")] string mode,
        [FromQuery(Name = "r")] int ranked
    )
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return BadRequest("no");

        var result = await beatmapService.SearchBeatmapSet(session, page + 1, query, mode == "-1" ? "" : mode, ranked);

        if (result == null)
            return BadRequest("no");

        return Ok(result);
    }

    [HttpGet(RequestType.OsuSearchSet)]
    public async Task<IActionResult> SearchBySetId(
        [FromQuery(Name = "u")] string username,
        [FromQuery(Name = "h")] string passhash,
        [FromQuery(Name = "s")] int? setId,
        [FromQuery(Name = "b")] int? beatmapId,
        [FromQuery(Name = "c")] string? beatmapHash
    )
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return BadRequest("no");

        var result = await beatmapService.SearchBeatmap(session, setId, beatmapId, beatmapHash);

        return Ok(result);
    }

    [HttpGet("/d/{id}")]
    public IActionResult DownloadBeatmapSet(string id)
    {
        return Redirect($"https://catboy.best/d/{id}");
    }
}

[ApiController]
[Subdomain("b")]
public class BeatmapAssetsController : ControllerBase
{
    [HttpGet("{type}/{path}")]
    public IActionResult RedirectToResource(string type, string path)
    {
        return Redirect($"https://b.ppy.sh/{type}/{path}");
    }
}