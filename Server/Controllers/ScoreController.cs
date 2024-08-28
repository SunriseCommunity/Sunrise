using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Controllers;

[Route("/web")]
[Subdomain("osu")]
public class ScoreController : ControllerBase
{
    [HttpPost(RequestType.OsuSubmitScore)]
    public async Task<IActionResult> Submit(
        [FromForm(Name = "pass")] string passhash,
        [FromForm(Name = "bmk")] string beatmapHash,
        [FromForm(Name = "c1")] string uniqueIds,
        [FromForm(Name = "st")] int scoreTime,
        [FromForm(Name = "ft")] int scoreFailTime,
        [FromForm(Name = "osuver")] string osuVersion,
        // [FromForm(Name = "s")] byte[] clientHashB64,
        [FromForm(Name = "iv")] string iv,
        [FromForm(Name = "score")] string scoreEncoded,
        [FromForm(Name = "x")] string? isScoreNotComplete,
        [FromForm(Name = "score")] IFormFile? replayFile = null,
        [FromForm(Name = "sbk")] string? storyboardHash = null
    )
    {
        if (isScoreNotComplete == "1")
            return Ok("error: no");

        var scoreSerialized = Parsers.ParseSubmittedScore(osuVersion, scoreEncoded, iv);
        var username = scoreSerialized.Split(':')[1].Trim();

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        var result = await ScoreService.SubmitScore(session, scoreSerialized, beatmapHash, scoreTime, scoreFailTime, osuVersion, uniqueIds, replayFile, storyboardHash);
        return await Task.FromResult<IActionResult>(Ok(result));
    }

    [HttpGet(RequestType.OsuGetScores)]
    public async Task<IActionResult> GetScores(
        [FromQuery(Name = "us")] string username,
        [FromQuery(Name = "ha")] string passhash,
        [FromQuery(Name = "s")] string? fromEditor,
        [FromQuery(Name = "vv")] string leaderboardVersion,
        [FromQuery(Name = "v")] LeaderboardType leaderboardType,
        [FromQuery(Name = "c")] string beatmapHash,
        [FromQuery(Name = "f")] string filename,
        [FromQuery(Name = "i")] int setId,
        [FromQuery(Name = "m")] GameMode mode,
        [FromQuery(Name = "mods")] Mods mods
    )
    {
        if (fromEditor == "1" || leaderboardVersion != "4")
            return Ok("error: pass");

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        var result = await ScoreService.GetBeatmapScores(session, setId, mode, mods, leaderboardType, beatmapHash, filename);

        return Ok(result);
    }

    [HttpGet(RequestType.OsuGetReplay)]
    public async Task<IActionResult> GetReplay(
        [FromQuery(Name = "c")] int scoreId
    )
    {
        var result = await AssetService.GetOsuReplayBytes(scoreId);
        if (result == null)
            return Ok("error: no-replay");

        return new FileContentResult(result, "application/octet-stream");
    }
}