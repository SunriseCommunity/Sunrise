using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;

namespace Sunrise.Server.API.Controllers;

[Route("score/{id:int}")]
[Subdomain("api")]
[ResponseCache(VaryByHeader = "Authorization", Duration = 300)]
public class ScoreController : ControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> GetScore(int id)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var score = await database.GetScore(id);
        if (score == null)
            return NotFound(new ErrorResponse("Score not found"));

        return Ok(new ScoreResponse(score));
    }

    [HttpGet("replay")]
    public async Task<IActionResult> GetScoreReplay(int id)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized(new ErrorResponse("Invalid session"));

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var score = await database.GetScore(id);
        if (score?.ReplayFileId == null)
            return NotFound(new ErrorResponse("Score or replay not found"));

        var replay = await database.GetReplay(score.ReplayFileId.Value);
        if (replay == null)
            return NotFound(new ErrorResponse("Replay not found"));

        var replayFile = new ReplayFile(score, replay);
        var replayStream = await replayFile.ReadReplay();
        var replayFileName = await replayFile.GetFileName(session);

        return File(replayStream.ToArray(), "application/octet-stream", replayFileName);
    }

    [HttpGet("/score/top")]
    public async Task<IActionResult> GetTopScores([FromQuery(Name = "mode")] int mode,
        [FromQuery(Name = "limit")] int? limit = 15,
        [FromQuery(Name = "page")] int? page = 0)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        if (mode is < 0 or > 3) return BadRequest(new ErrorResponse("Invalid mode parameter"));

        if (limit is < 1 or > 100) return BadRequest(new ErrorResponse("Invalid limit parameter"));

        var scores = await database.GetTopScores((GameMode)mode);

        var offsetScores = scores.Skip(page * limit ?? 0).Take(limit ?? 50).Select(score => new ScoreResponse(score))
            .ToList();

        return Ok(new ScoresResponse(offsetScores, scores.Count));
    }
}