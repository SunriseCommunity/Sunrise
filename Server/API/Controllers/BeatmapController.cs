using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Managers;
using AuthService = Sunrise.Server.API.Services.AuthService;

namespace Sunrise.Server.API.Controllers;

[Subdomain("api")]
[ResponseCache(VaryByHeader = "Authorization", Duration = 300)]
public class BeatmapController : ControllerBase
{
    [HttpGet("beatmap/{id:int}")]
    [HttpGet("beatmapset/{beatmapSet:int}/{id:int}")]
    public async Task<IActionResult> GetBeatmap(int id)
    {
        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapId: id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);

        if (beatmap == null)
            return NotFound(new ErrorResponse("Beatmap not found"));

        return Ok(new BeatmapResponse(beatmap, beatmapSet));
    }

    [HttpGet("beatmapset/{id:int}")]
    public async Task<IActionResult> GetBeatmapSet(int id)
    {
        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        return Ok(new BeatmapSetResponse(beatmapSet));
    }

    [HttpGet("beatmapset/{id:int}")]
    public async Task<IActionResult> BeatmapSetFavourite(int id, [FromQuery] bool favourite)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Unauthorized"));

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        if (favourite)
            await database.AddFavouriteBeatmap(session.User.Id, id);
        else
            await database.RemoveFavouriteBeatmap(session.User.Id, id);

        return new OkResult();
    }

    [HttpGet("beatmapset/{id:int}/favourited")]
    public async Task<IActionResult> GetFavourited(int id)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Unauthorized"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var favourited = await database.IsBeatmapSetFavourited(session.User.Id, id);

        return Ok(new { favourited });
    }
}