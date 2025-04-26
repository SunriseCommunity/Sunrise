using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Managers;
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

[Route("score/{id:int}")]
[Subdomain("api")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
public class ScoreController(DatabaseService database, SessionManager sessionManager, SessionRepository sessions) : ControllerBase
{
    [HttpGet("")]
    [ResponseCache(Duration = 300)]
    [EndpointDescription("Get score")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScoreResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScore(int id)
    {
        var score = await database.Scores.GetScore(id,
            new QueryOptions(true)
            {
                QueryModifier = query => query.Cast<Score>().IncludeUser()
            });
        
        if (score == null)
            return NotFound(new ErrorResponse("Score not found"));
        
        score = (await database.Scores.EnrichScoresWithLeaderboardPosition([score])).First();

        return Ok(new ScoreResponse(sessions, score));
    }

    [HttpGet("replay")]
    [ResponseCache(VaryByHeader = "Authorization", Duration = 300)]
    [EndpointDescription("Get score replay file")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScoreReplay(int id)
    {
        var session = await sessionManager.GetSessionFromRequest(Request);
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var score = await database.Scores.GetScore(id, new QueryOptions(true));
        if (score?.ReplayFileId == null)
            return NotFound(new ErrorResponse("Score or replay not found"));

        var replay = await database.Scores.Files.GetReplayFile(score.ReplayFileId.Value);
        if (replay == null)
            return NotFound(new ErrorResponse("Replay not found"));

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
    public async Task<IActionResult> GetTopScores([FromQuery(Name = "mode")] GameMode mode,
        [FromQuery(Name = "limit")] int? limit = 15,
        [FromQuery(Name = "page")] int? page = 1)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse("One or more required fields are invalid"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));
        if (page is <= 0) return BadRequest(new ErrorResponse("Invalid page parameter"));

        var (scores, _) = await database.Scores.GetBestScoresByGameMode(mode,
            new QueryOptions(true, new Pagination(page!.Value, limit!.Value))
            {
                QueryModifier = query => query.Cast<Score>().IncludeUser()
            });

        scores = await database.Scores.EnrichScoresWithLeaderboardPosition(scores);

        var parsedScores = scores.Select(score => new ScoreResponse(sessions, score)).ToList();

        return Ok(new ScoresResponse(parsedScores, scores.Count));
    }
}