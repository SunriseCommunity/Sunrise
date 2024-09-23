using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.API.Managers;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Controllers;

[Route("score/{id:int}")]
[Subdomain("api")]
[ResponseCache(VaryByHeader = "Authorization", Duration = 3600)]
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

        var score = await database.GetScore(id: id);
        if (score == null)
            return NotFound(new ErrorResponse("Score not found"));

        var replay = await database.GetReplay(score.ReplayFileId);
        if (replay == null)
            return NotFound(new ErrorResponse("Replay not found"));

        var replayFile = new ReplayFile(score, replay);
        var replayStream = await replayFile.ReadReplay();
        var replayFileName = await replayFile.GetFileName(session);

        return File(replayStream.ToArray(), "application/octet-stream", replayFileName);
    }
}