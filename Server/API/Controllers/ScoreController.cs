using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Attributes;
using Sunrise.Server.Data;
using Sunrise.Server.Objects;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Controllers;

[Route("score/{id:int}")]
[Subdomain("api")]
public class ScoreController : ControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> GetScore(int id)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var score = await database.GetScore(id);
        if (score == null)
            return NotFound("Score not found");

        // TODO: Proper to json covertion
        return Ok(new ScoreResponse(score));
    }

    [HttpGet("replay")]
    public async Task<IActionResult> GetScoreReplay(int id)
    {
        var session = await Request.GetSessionFromRequest();
        if (session == null)
            return Unauthorized("Invalid session");

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var score = await database.GetScore(id: id);
        if (score == null)
            return NotFound("Score not found");

        var replay = await database.GetReplay(score.ReplayFileId);
        if (replay == null)
            return NotFound("Replay not found");

        var replayFile = new ReplayFile(score, replay);
        var replayStream = await replayFile.ReadReplay();
        var replayFileName = await replayFile.GetFileName(session);

        return File(replayStream.ToArray(), "application/octet-stream", replayFileName);
    }
}