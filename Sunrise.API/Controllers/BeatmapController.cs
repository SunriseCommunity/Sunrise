using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.API.Enums;
using Sunrise.API.Extensions;
using Sunrise.API.Objects;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.API.Utils;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using WebSocketManager = Sunrise.API.Managers.WebSocketManager;

namespace Sunrise.API.Controllers;

[Subdomain("api")]
[ResponseCache(VaryByHeader = "Authorization", Duration = 300)]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
public class BeatmapController(DatabaseService database, BeatmapService beatmapService, CalculatorService calculatorService, SessionRepository sessions, WebSocketManager webSocketManager) : ControllerBase
{
    [HttpGet("beatmap/{id:int}")]
    [HttpGet("beatmapset/{beatmapSet:int}/{id:int}")]
    [EndpointDescription("Get beatmap")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BeatmapResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmap(int id, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = HttpContext.GetCurrentSession();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapId: id, ct: ct);
        if (beatmapSetResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetResult.Error);

        var beatmapSet = beatmapSetResult.Value;

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);

        if (beatmap == null)
            return NotFound(new ErrorResponse("Beatmap not found"));

        return Ok(new BeatmapResponse(sessions, beatmap, beatmapSet));
    }

    [HttpGet("beatmap/{id:int}/pp")]
    [HttpGet("beatmapset/{beatmapSet:int}/{id:int}/pp")]
    [EndpointDescription("Get beatmap performance")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PerformanceAttributes), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmapPerformance(int id,
        [FromQuery(Name = "mods")] IEnumerable<Mods>? mods = null,
        [FromQuery(Name = "mode")] GameMode? gameMode = null,
        [FromQuery(Name = "combo")] int? combo = null,
        [FromQuery(Name = "misses")] int? misses = null,
        [FromQuery(Name = "accuracy")] float? accuracy = null,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        if (gameMode is < GameMode.Standard or > GameMode.Mania)
            return BadRequest(new ErrorResponse("Invalid game mode"));

        if (accuracy is < 0 or > 100)
            return BadRequest(new ErrorResponse("Invalid accuracy"));

        var session = HttpContext.GetCurrentSession();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapId: id, ct: ct);
        if (beatmapSetResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetResult.Error);

        var beatmapSet = beatmapSetResult.Value;

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);

        if (beatmap == null)
            return NotFound(new ErrorResponse("Beatmap not found"));

        var modsEnum = (mods ?? Array.Empty<Mods>()).Aggregate(Mods.None, (current, mod) => current | mod);

        var performance = await calculatorService.CalculateBeatmapPerformance(session, id, gameMode ?? (GameMode)beatmap.ModeInt, modsEnum, combo, misses, accuracy);

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
        [FromQuery(Name = "mods")] IEnumerable<Mods>? mods = null,
        [FromQuery(Name = "limit")] int limit = 50,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = HttpContext.GetCurrentSession();

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapId: id, ct: ct);
        if (beatmapSetResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetResult.Error);

        var beatmapSet = beatmapSetResult.Value;

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == id);
        if (beatmap == null || beatmap.IsScoreable == false)
            return Ok(new ScoresResponse([], 0));


        var modsEnum = (mods ?? Array.Empty<Mods>()).Aggregate(Mods.None, (current, mod) => current | mod);

        var (scores, totalScores) = await database.Scores.GetBeatmapScores(beatmap.Checksum,
            mode,
            mods is null ? LeaderboardType.Global : LeaderboardType.GlobalIncludesMods,
            modsEnum,
            options: new QueryOptions(new Pagination(1, limit))
            {
                QueryModifier = query => query.Cast<Score>().IncludeUser()
            },
            ct: ct);

        scores = await database.Scores.EnrichScoresWithLeaderboardPosition(scores, ct);

        var parsedScores = scores.Select(score => new ScoreResponse(sessions, score)).ToList();
        return Ok(new ScoresResponse(parsedScores, totalScores));
    }

    [HttpGet("beatmapset/{id:int}")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Get beatmapset")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BeatmapSetResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmapSet(int id, CancellationToken ct = default)
    {
        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = HttpContext.GetCurrentSession();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, id, ct: ct);
        if (beatmapSetResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetResult.Error);

        var beatmapSet = beatmapSetResult.Value;

        return Ok(new BeatmapSetResponse(sessions, beatmapSet));
    }

    [Authorize]
    [HttpPost("beatmapset/{id:int}/hype")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Hype beatmapset")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HypeBeatmapSet(int id, CancellationToken ct = default)
    {
        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = HttpContext.GetCurrentSession();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, id, ct: ct);
        if (beatmapSetResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetResult.Error);

        var beatmapSet = beatmapSetResult.Value;

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return Unauthorized(new ErrorResponse("User not found"));

        var hypeBeatmapSetResult = await database.Beatmaps.Hypes.AddBeatmapHypeFromUserInventory(user, beatmapSet.Id);
        if (hypeBeatmapSetResult.IsFailure)
            return BadRequest(new ErrorResponse(hypeBeatmapSetResult.Error));

        return new OkResult();
    }

    [Authorize("RequireBat")]
    [HttpGet("beatmapset/get-hyped-sets")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Returns beatmapsets with active hype train")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HypedBeatmapSetsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHypedBeatmapSets(
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var session = HttpContext.GetCurrentSession();

        var (beatmapSetIdsWithHypeCount, totalCount) = await database.Beatmaps.Hypes.GetHypedBeatmaps(new QueryOptions(true, new Pagination(page, limit)), ct);

        var result = beatmapSetIdsWithHypeCount.Select(async g =>
        {
            var (beatmapSetId, hypeCount) = g;

            var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapSetId, ct: ct);
            if (beatmapSetResult.IsFailure)
                return null;

            return new HypedBeatmapSetResponse(sessions, beatmapSetResult.Value, hypeCount);
        }).Select(task => task.Result).Where(x => x != null).Select(x => x!).ToList();

        return Ok(new HypedBeatmapSetsResponse(result, totalCount));
    }

    [HttpGet("beatmapset/{id:int}/hype")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Get beatmapset hype count")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BeatmapSetHypeCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmapSetHypeCounter(int id, CancellationToken ct = default)
    {
        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap set id"));

        var session = HttpContext.GetCurrentSession();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, id, ct: ct);
        if (beatmapSetResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetResult.Error);

        var beatmapSet = beatmapSetResult.Value;

        var beatmapSetHypeCount = await database.Beatmaps.Hypes.GetBeatmapHypeCount(beatmapSet.Id);

        return Ok(new BeatmapSetHypeCountResponse
        {
            CurrentHypes = beatmapSetHypeCount
        });
    }

    [Authorize("RequireBat")]
    [HttpGet("beatmapset/{id:int}/events")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Get beatmapset related events")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(BeatmapSetEventsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmapSetEvents(int id,
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap set id"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var (beatmapSetEvents, totalCount) = await database.Events.Beatmaps.GetBeatmapSetEvents(id,
            new QueryOptions(true, new Pagination(page, limit))
            {
                QueryModifier = q => q.Cast<EventBeatmap>().IncludeExecutor()
            },
            ct);

        var session = HttpContext.GetCurrentSession();

        var beatmapSetsResult = await beatmapService.GetBeatmapSets(session, beatmapSetEvents.Select(e => e.BeatmapSetId).ToList(), ct);

        if (beatmapSetsResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetsResult.Error);

        var beatmapSets = beatmapSetsResult.Value;

        var events = beatmapSetEvents.Select(e =>
        {
            var beatmapSet = beatmapSets.First(v => v.Id == e.BeatmapSetId);

            return new BeatmapEventResponse(sessions, e, new BeatmapSetResponse(sessions, beatmapSet));
        }).ToList();

        return Ok(new BeatmapSetEventsResponse(events, totalCount));
    }

    [Authorize("RequireBat")]
    [HttpGet("beatmapset/events")]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Get beatmapsets related events")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(BeatmapSetEventsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeatmapSetsEvents(
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var (beatmapSetEvents, totalCount) = await database.Events.Beatmaps.GetBeatmapSetEvents(null,
            new QueryOptions(true, new Pagination(page, limit))
            {
                QueryModifier = q => q.Cast<EventBeatmap>().IncludeExecutor()
            },
            ct
        );

        var session = HttpContext.GetCurrentSession();

        var beatmapSetsResult = await beatmapService.GetBeatmapSets(session, beatmapSetEvents.Select(e => e.BeatmapSetId).ToList(), ct);

        if (beatmapSetsResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetsResult.Error);

        var beatmapSets = beatmapSetsResult.Value;

        var events = beatmapSetEvents.Select(e =>
        {
            var beatmapSet = beatmapSets.First(v => v.Id == e.BeatmapSetId);

            return new BeatmapEventResponse(sessions, e, new BeatmapSetResponse(sessions, beatmapSet));
        }).ToList();

        return Ok(new BeatmapSetEventsResponse(events, totalCount));
    }

    [HttpPost("beatmapset/{id:int}/favourited")]
    [Authorize]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Add/remove beatmapset from users favourites")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateBeatmapsetFavouriteStatus(int id, [FromBody] EditBeatmapsetFavouriteStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var session = HttpContext.GetCurrentSession();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, id);
        if (beatmapSetResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetResult.Error);

        if (request.Favourited)
            await database.Users.Favourites.AddFavouriteBeatmap(session.UserId, id);
        else
            await database.Users.Favourites.RemoveFavouriteBeatmap(session.UserId, id);

        return new OkResult();
    }

    [HttpGet("beatmapset/{id:int}/favourited")]
    [Authorize]
    [ResponseCache(Duration = 0)]
    [EndpointDescription("Check if beatmapset is favourited by current user")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FavouritedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFavourited(int id, CancellationToken ct = default)
    {
        if (id < 0)
            return BadRequest(new ErrorResponse("Invalid beatmap id"));

        var user = HttpContext.GetCurrentUser();
        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        var favourited = await database.Users.Favourites.IsBeatmapSetFavourited(user.Id, id, ct);

        return Ok(new FavouritedResponse
        {
            Favourited = favourited
        });
    }

    [Authorize("RequireBat")]
    [HttpPost("beatmap/update-custom-status")]
    [EndpointDescription("Updates beatmap custom status. Use \'Unknown\' to reset beatmap custom status")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBeatmapStatus([FromBody] UpdateBeatmapsCustomStatusRequest request)
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        var session = HttpContext.GetCurrentSession();
        var user = HttpContext.GetCurrentUser();

        if (user == null)
            return BadRequest(new ErrorResponse("Invalid session"));

        foreach (var id in request.Ids)
        {
            var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapId: id);
            if (beatmapSetResult.IsFailure)
                return ActionResultUtil.ActionErrorResult(beatmapSetResult.Error);

            var beatmapSet = beatmapSetResult.Value;
            if (beatmapSet == null)
                return ActionResultUtil.ActionErrorResult(beatmapSetResult.Error);

            var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == id);

            if (beatmap == null)
            {
                return BadRequest(new ErrorResponse("Beatmap not found."));
            }

            var resetBeatmapStatus = request.Status == BeatmapStatusWeb.Unknown;
            var changeBeatmapSetStatusResult = await beatmapService.ChangeBeatmapCustomStatus(
                user,
                beatmap,
                resetBeatmapStatus ? null : request.Status,
                resetBeatmapStatus ? true : null
            );

            if (changeBeatmapSetStatusResult.IsFailure)
                return BadRequest(new ErrorResponse(changeBeatmapSetStatusResult.Error));

            var oldStatus = beatmap.StatusGeneric;

            if (oldStatus != request.Status && !resetBeatmapStatus)
            {
                beatmapSet.UpdateBeatmapRanking([changeBeatmapSetStatusResult.Value ?? throw new InvalidOperationException()]);
                webSocketManager.BroadcastJsonAsync(new WebSocketMessage(WebSocketEventType.CustomBeatmapStatusChanged, new CustomBeatmapStatusChangeResponse(new BeatmapResponse(sessions, beatmap, beatmapSet), request.Status, oldStatus, new UserResponse(sessions, user))));
            }
        }

        return new OkResult();
    }

    [HttpGet("/beatmapset/search")]
    [EndpointDescription("Search beatmapsets")]
    [ResponseCache(Duration = 0)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BeatmapSetsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchBeatmapsets(
        [FromQuery(Name = "query")] string? query,
        [FromQuery(Name = "status")] BeatmapStatusWeb[]? status,
        [FromQuery(Name = "mode")] GameMode? mode,
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "page")] int page = 1,
        CancellationToken ct = default
    )
    {
        if (ModelState.IsValid != true)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        if (page <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var session = HttpContext.GetCurrentSession();

        var beatmapSetStatus = status?.Any() == true ? string.Join("&status=", status.Select(s => (int)s)) : null;
        var beatmapSetGameMode = mode.HasValue ? (int)mode : -1;

        if (mode is > GameMode.Mania)
            return BadRequest(new ErrorResponse("Invalid game mode"));

        var beatmapSetsResult = await beatmapService.SearchBeatmapSets(session,
            beatmapSetStatus,
            beatmapSetGameMode.ToString(),
            query,
            new Pagination(page - 1, limit),
            ct);

        if (beatmapSetsResult.IsFailure)
            return ActionResultUtil.ActionErrorResult(beatmapSetsResult.Error);

        var beatmapSets = beatmapSetsResult.Value;

        return Ok(new BeatmapSetsResponse(beatmapSets?.Select(s => new BeatmapSetResponse(sessions, s)).ToList() ?? [], null));
    }
}