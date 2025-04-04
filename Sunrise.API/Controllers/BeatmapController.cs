using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.API.Managers;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using AuthService = Sunrise.API.Services.AuthService;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.API.Controllers;

[Subdomain("api")]
[ResponseCache(VaryByHeader = "Authorization", Duration = 300)]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
public class BeatmapController(SessionManager sessionManager, DatabaseService database, BeatmapService beatmapService, CalculatorService calculatorService, SessionRepository sessions) : ControllerBase
{
    [HttpGet("beatmap/{id:int}")]
    [HttpGet("beatmapset/{beatmapSet:int}/{id:int}")]
    [EndpointDescription("Get beatmap")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BeatmapResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmap(int id)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = await sessionManager.GetSessionFromRequest(Request) ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await beatmapService.GetBeatmapSet(session, beatmapId: id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);

        if (beatmap == null)
            return NotFound(new ErrorResponse("Beatmap not found"));

        return Ok(new BeatmapResponse(session, beatmap, beatmapSet));
    }

    [HttpGet("beatmap/{id:int}/pp")]
    [HttpGet("beatmapset/{beatmapSet:int}/{id:int}/pp")]
    [EndpointDescription("Get beatmap performance")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PerformanceAttributes), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmapPerformance(int id, [FromQuery(Name = "mods")] Mods? mods = null, [FromQuery(Name = "mode")] int? gameMode = null, [FromQuery(Name = "combo")] int? combo = null, [FromQuery(Name = "misses")] int? misses = null)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        if (gameMode is < 0 or > 3)
            return BadRequest(new ErrorResponse("Invalid game mode"));

        var session = await sessionManager.GetSessionFromRequest(Request) ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await beatmapService.GetBeatmapSet(session, beatmapId: id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);

        if (beatmap == null)
            return NotFound(new ErrorResponse("Beatmap not found"));

        var performance = await calculatorService.CalculateBeatmapPerformance(session, id, gameMode ?? beatmap.ModeInt, mods ?? Mods.None, combo, misses);

        if (performance.IsFailure)
            return BadRequest(new ErrorResponse(performance.Error.Message));

        return Ok(performance.Value);
    }

    [HttpGet("beatmap/{id:int}/leaderboard")]
    [HttpGet("beatmapset/{beatmapSet:int}/{id:int}/leaderboard")]
    [ResponseCache(Duration = 10)]
    [EndpointDescription("Get beatmap leaderboard")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScoresResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmapLeaderboard(int id,
        [FromQuery(Name = "mode")] GameMode mode,
        [FromQuery(Name = "mods")] Mods? mods = null,
        [FromQuery(Name = "limit")] int limit = 50)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = await sessionManager.GetSessionFromRequest(Request) ?? AuthService.GenerateIpSession(Request);

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var beatmapSet = await beatmapService.GetBeatmapSet(session, beatmapId: id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);
        if (beatmap == null || beatmap.IsScoreable == false)
            return Ok(new ScoresResponse([], 0));

        var (scores, totalScores) = await database.Scores.GetBeatmapScores(beatmap.Checksum,
            mode,
            mods is null ? LeaderboardType.Global : LeaderboardType.GlobalWithMods,
            mods,
            options: new QueryOptions(new Pagination(1, limit)));

        foreach (var score in scores)
        {
            await database.DbContext.Entry(score).Reference(s => s.User).LoadAsync();
        }

        var parsedScores = scores.Select(score => new ScoreResponse(database, sessions, score)).ToList();
        return Ok(new ScoresResponse(parsedScores, totalScores));
    }

    [HttpGet("beatmapset/{id:int}")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Add/remove beatmapset from users favourites. Provide favourite boolean query to add or remove beatmapset from users favourites")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(BeatmapSetResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmapSet(int id, [FromQuery] bool? favourite)
    {
        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = await sessionManager.GetSessionFromRequest(Request) ?? AuthService.GenerateIpSession(Request);

        var beatmapSet = await beatmapService.GetBeatmapSet(session, id);
        if (beatmapSet == null)
            return NotFound(new ErrorResponse("Beatmap set not found"));

        if (favourite.HasValue)
        {
            if (session.IsGuest)
                return Unauthorized(new ErrorResponse("Unauthorized"));

            if (favourite.Value)
                await database.Users.Favourites.AddFavouriteBeatmap(session.UserId, id);
            else
                await database.Users.Favourites.RemoveFavouriteBeatmap(session.UserId, id);

            return new OkResult();
        }

        return Ok(new BeatmapSetResponse(session, beatmapSet));
    }

    [HttpGet("beatmapset/{id:int}/favourited")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Check if beatmapset is favourited by current user")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FavouritedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFavourited(int id)
    {
        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = await sessionManager.GetSessionFromRequest(Request);
        if (session == null)
            return Unauthorized(new ErrorResponse("Unauthorized"));

        var favourited = await database.Users.Favourites.IsBeatmapSetFavourited(session.UserId, id);

        return Ok(new FavouritedResponse
        {
            Favourited = favourited
        });
    }
}