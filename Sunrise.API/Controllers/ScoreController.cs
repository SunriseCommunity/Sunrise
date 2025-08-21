using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Extensions;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Repositories;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.API.Controllers;

[ApiController]
[Route("score/{id:int}")]
[Subdomain("api")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status400BadRequest)]
public class ScoreController(DatabaseService database, SessionRepository sessions) : ControllerBase
{
    [HttpGet("")]
    [ResponseCache(Duration = 300)]
    [EndpointDescription("Get score")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScoreResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScore([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var score = await database.Scores.GetScore(id,
            new QueryOptions(true)
            {
                QueryModifier = query => query.Cast<Score>().IncludeUser()
            },
            ct);

        if (score == null)
            return Problem(ApiErrorResponse.Detail.ScoreNotFound, statusCode: StatusCodes.Status404NotFound);

        score = (await database.Scores.EnrichScoresWithLeaderboardPosition([score], ct)).First();

        return Ok(new ScoreResponse(sessions, score));
    }

    [HttpGet("replay")]
    [Authorize]
    [ResponseCache(VaryByHeader = "Authorization", Duration = 300)]
    [EndpointDescription("Get score replay file")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScoreReplay([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var session = HttpContext.GetCurrentSession();

        var score = await database.Scores.GetScore(id, new QueryOptions(true), ct);

        if (score == null)
            return Problem(ApiErrorResponse.Detail.ScoreNotFound, statusCode: StatusCodes.Status404NotFound);

        if (score.ReplayFileId == null)
            return Problem(ApiErrorResponse.Detail.ReplayNotFound, statusCode: StatusCodes.Status404NotFound);

        var replay = await database.Scores.Files.GetReplayFile(score.ReplayFileId.Value, ct);
        if (replay == null)
            return Problem(ApiErrorResponse.Detail.ReplayNotFound, statusCode: StatusCodes.Status404NotFound);

        var replayFile = new ReplayFile(score, replay);
        var replayStream = await replayFile.ReadReplay();
        var replayFileName = await replayFile.GetFileName(session);

        Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
        return File(replayStream.ToArray(), "application/octet-stream", replayFileName);
    }

    [HttpGet("/score/top")]
    [ResponseCache(Duration = 30)]
    [EndpointDescription("Get best scores on the server")]
    [ProducesResponseType(typeof(ScoresResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopScores(
        [FromQuery(Name = "mode")] GameMode mode,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int? limit = 15,
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int? page = 1,
        CancellationToken ct = default)
    {
        var (scores, totalCount) = await database.Scores.GetBestScoresByGameMode(mode,
            new QueryOptions(true, new Pagination(page!.Value, limit!.Value))
            {
                QueryModifier = query => query.Cast<Score>().IncludeUser()
            },
            ct);

        scores = await database.Scores.EnrichScoresWithLeaderboardPosition(scores, ct);

        var parsedScores = scores.Select(score => new ScoreResponse(sessions, score)).ToList();

        return Ok(new ScoresResponse(parsedScores, totalCount));
    }
}