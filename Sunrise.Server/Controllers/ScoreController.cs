using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using osu.Shared;
using Serilog;
using Sunrise.Server.Attributes;
using Sunrise.Server.Services;
using Sunrise.Server.Services.Helpers.Scores;
using Sunrise.Server.Utils;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Controllers;

[ServerHttpTrace]
[Route("/web")]
[Subdomain("osu")]
[ApiExplorerSettings(IgnoreApi = true)]
public class ScoreController(ScoreService scoreService, AssetBanchoService assetBanchoService, SessionRepository sessions) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, DateTime> _processingScores = new();
    private static readonly TimeSpan ScoreProcessingTimeout = TimeSpan.FromMinutes(10);

    [HttpPost(RequestType.OsuSubmitScore)]
    public async Task<IActionResult> Submit(
        [FromForm(Name = "pass")] string passhash,
        [FromForm(Name = "bmk")] string beatmapHash,
        [FromForm(Name = "st")] int scoreTime,
        [FromForm(Name = "ft")] int scoreFailTime,
        [FromForm(Name = "osuver")] string osuVersion,
        [FromForm(Name = "s")] string clientHashEncoded,
        [FromForm(Name = "iv")] string iv,
        [FromForm(Name = "score")] string scoreEncoded,
        [FromForm(Name = "x")] string? isScoreNotComplete,
        [FromForm(Name = "score")] IFormFile? replayFile = null,
        [FromForm(Name = "sbk")] string? storyboardHash = null
    )
    {
        var scoreSerialized = ServerParsers.ParseRijndaelString(osuVersion, iv, scoreEncoded);
        var clientHash = ServerParsers.ParseRijndaelString(osuVersion, iv, clientHashEncoded);
        var username = scoreSerialized.Split(':')[1].Trim();

        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session,
                scoreSerialized,
                "SubmitScore: Invalid session or passhash mismatch");

            return Ok("error: pass");
        }

        var scoreHash = scoreSerialized.Split(':')[2];

        // TODO: Hopefully this will be replaced by writing scores which are being processed to the database (check BeatmapService TODOs), since right now this is not really scalable. 
        var isScoreAlreadyBeingProcessed = _processingScores.TryGetValue(scoreHash, out var existingTimestamp) &&
                                           DateTime.UtcNow - existingTimestamp < ScoreProcessingTimeout;

        if (isScoreAlreadyBeingProcessed)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session,
                scoreSerialized,
                "Duplicate score submission while previous is still processing");

            return Ok("error: no");
        }

        var isScoreAddedToProcessing = _processingScores.TryAdd(scoreHash, DateTime.UtcNow);

        if (!isScoreAddedToProcessing)
        {
            Log.Warning("Failed to add score hash {ScoreHash} to processing dictionary for user {Username}. Another submission might be processing concurrently.", scoreHash, username);
        }

        try
        {
            var result = await scoreService.SubmitScore(session,
                scoreSerialized,
                beatmapHash,
                scoreTime,
                scoreFailTime,
                osuVersion,
                clientHash,
                replayFile,
                storyboardHash);

            return Ok(result);
        }
        finally
        {
            _processingScores.TryRemove(scoreHash, out _);
        }
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
        [FromQuery(Name = "mods")] Mods mods,
        CancellationToken ct = default
    )
    {
        if (fromEditor == "1" || leaderboardVersion != "4")
            return Ok("error: pass");

        if (!sessions.TryGetSession(username, passhash, out var session) || session == null)
            return Ok("error: pass");

        var result =
            await scoreService.GetBeatmapScores(session, setId, mode, mods, leaderboardType, beatmapHash, filename, ct);

        return Ok(result);
    }

    [HttpGet(RequestType.OsuGetReplay)]
    public async Task<IActionResult> GetReplay(
        [FromQuery(Name = "c")] int scoreId, CancellationToken ct = default
    )
    {
        var getReplayResult = await assetBanchoService.GetOsuReplayBytes(scoreId, ct);
        if (getReplayResult.IsFailure)
            return Ok("error: no-replay");

        return new FileContentResult(getReplayResult.Value, "application/octet-stream");
    }
}