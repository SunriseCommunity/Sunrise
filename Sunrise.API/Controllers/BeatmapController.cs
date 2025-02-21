using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.API.Managers;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Repositories;
using AuthService = Sunrise.API.Services.AuthService;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.API.Controllers;

[Subdomain("api")]
[ResponseCache(VaryByHeader = "Authorization", Duration = 300)]
public class BeatmapController : ControllerBase
{
    [HttpGet("beatmap/{id:int}")]
    [HttpGet("beatmapset/{beatmapSet:int}/{id:int}")]
    public async Task<IActionResult> GetBeatmap(int id)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await BeatmapRepository.GetBeatmapSet(session, beatmapId: id);
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
        [FromQuery(Name = "mode")] string mode,
        [FromQuery(Name = "mods")] string? mods = null,
        [FromQuery(Name = "limit")] int limit = 50)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var modeEnum = GameMode.Standard;
        if (mode != null && Enum.TryParse(mode, out modeEnum) == false)
            return BadRequest(new ErrorResponse("Invalid mode parameter"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var modsEnum = Mods.None;
        if (mods != null && Enum.TryParse(mods, out modsEnum) == false)
            return BadRequest(new ErrorResponse("Invalid mods parameter"));

        var beatmapSet = await BeatmapRepository.GetBeatmapSet(session, beatmapId: id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);
        if (beatmap?.IsScoreable == false)
            return Ok(new ScoresResponse([], 0));

        var scores = await database.ScoreService.GetBeatmapScores(beatmap.Checksum, modeEnum, modsEnum == Mods.None && mods == null ? LeaderboardType.Global : LeaderboardType.GlobalWithMods, modsEnum);

        var limitedScores = scores.SortScoresByTheirScoreValue().Take(limit).Select(score => new ScoreResponse(score, database.UserService.GetUser(score.UserId).Result)).ToList();
        return Ok(new ScoresResponse(limitedScores, scores.Count));
    }

    [HttpGet("beatmapset/{id:int}")]
    public async Task<IActionResult> GetBeatmapSet(int id, [FromQuery] bool? favourite)
    {
        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = await Request.GetSessionFromRequest() ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await BeatmapRepository.GetBeatmapSet(session, id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        if (favourite.HasValue)
        {
            if (session.User.Username == "Guest")
                return Unauthorized(new ErrorResponse("Unauthorized"));

            var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
            if (favourite.Value)
                await database.UserService.Favourites.AddFavouriteBeatmap(session.User.Id, id);
            else
                await database.UserService.Favourites.RemoveFavouriteBeatmap(session.User.Id, id);

            return new OkResult();
        }

        return Ok(new BeatmapSetResponse(session, beatmapSet));
    }

    [HttpGet("beatmapset/{id:int}/favourited")]
    public async Task<IActionResult> GetFavourited(int id)
    {
        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Unauthorized"));

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var favourited = await database.UserService.Favourites.IsBeatmapSetFavourited(session.User.Id, id);

        return Ok(new FavouritedResponse
        {
            Favourited = favourited
        });
    }
}