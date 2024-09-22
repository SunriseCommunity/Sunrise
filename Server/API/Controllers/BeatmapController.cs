using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Attributes;
using Sunrise.Server.Managers;
using AuthService = Sunrise.Server.API.Services.AuthService;

namespace Sunrise.Server.API.Controllers;

[Subdomain("api")]
[ResponseCache(VaryByHeader = "User-Agent", Duration = 3600)]
public class BeatmapController : ControllerBase
{
    [HttpGet("beatmap/{id:int}")]
    [HttpGet("beatmapset/{beatmapSet:int}/{id:int}")]
    public async Task<IActionResult> GetBeatmap(int id)
    {
        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapId: id);
        if (beatmapSet == null)
            return NotFound("Beatmap set not found");

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);

        if (beatmap == null)
            return NotFound("Beatmap not found");

        return Ok(new BeatmapResponse(beatmap, beatmapSet));
    }

    [HttpGet("beatmapset/{id:int}")]
    public async Task<IActionResult> GetBeatmapSet(int id)
    {
        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapId: id);
        if (beatmapSet == null)
            return NotFound("Beatmap set not found");

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);

        return Ok(new BeatmapSetResponse(beatmapSet));
    }
}