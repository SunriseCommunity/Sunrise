using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Managers;
using Sunrise.Server.Types.Enums;
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

        return Ok(new BeatmapResponse(session, beatmap, beatmapSet));
    }

    [HttpGet("beatmap/{id:int}/leaderboard")]
    [HttpGet("beatmapset/{beatmapSet:int}/{id:int}/leaderboard")]
    public async Task<IActionResult> GetBeatmapLeaderboard(int id,
        [FromQuery(Name = "mode")] string? mode,
        [FromQuery(Name = "mods")] string? mods = null,
        [FromQuery(Name = "limit")] int limit = 50)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var modeEnum = GameMode.Standard;
        if (mode != null && Enum.TryParse(mode, out modeEnum) == false)
            return BadRequest(new ErrorResponse("Invalid mode parameter"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var modsEnum = Mods.None;
        if (mods != null && Enum.TryParse(mods, out modsEnum) == false)
            return BadRequest(new ErrorResponse("Invalid mods parameter"));

        var scores = await database.GetBeatmapScoresById(id, modeEnum, modsEnum == Mods.None ? LeaderboardType.Global : LeaderboardType.GlobalWithMods, modsEnum, modsShouldEqual: false);

        var limitedScores = scores.Take(limit).Select(score => new ScoreResponse(score)).ToList();
        return Ok(new ScoresResponse(limitedScores, scores.Count));
    }

    [HttpGet("beatmapset/{id:int}")]
    public async Task<IActionResult> GetBeatmapSet(int id, [FromQuery] bool? favourite)
    {
        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        if (favourite.HasValue && session.User.Username != "Guest")
        {
            var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
            if (favourite.Value)
                await database.AddFavouriteBeatmap(session.User.Id, id);
            else
                await database.RemoveFavouriteBeatmap(session.User.Id, id);

            return new OkResult();
        }

        return Ok(new BeatmapSetResponse(session, beatmapSet));
    }

    [HttpGet("beatmapset/{id:int}/favourited")]
    public async Task<IActionResult> GetFavourited(int id)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Unauthorized"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var favourited = await database.IsBeatmapSetFavourited(session.User.Id, id);

        return Ok(new
        {
            favourited
        });
    }
}