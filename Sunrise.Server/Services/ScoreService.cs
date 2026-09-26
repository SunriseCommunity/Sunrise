using osu.Shared;
using Serilog;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Processing.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Services;

public class ScoreService(BeatmapService beatmapService, DatabaseService database, ScoreSubmissionHandler submissionTaskHandler)
{
    private const int OsuReplayFileHeaderSize = 24;

    [TraceExecution]
    public async Task<string> SubmitScore(Session session, string scoreSerialized, string beatmapHash,
        int scoreTime, int scoreFailTime, string osuVersion, string clientHash, IFormFile? replay,
        string? storyboardHash)
    {
        var scoreSubmittedAt = DateTime.UtcNow;

        var parsedScoreResult = scoreSerialized.TryParseBaseScore(scoreSubmittedAt);

        if (parsedScoreResult.IsFailure)
        {
            Log.Error("Failed to parse submitted score for user {UserId}: {Error}", session.UserId, parsedScoreResult.Error);
            return "error: no";
        }

        var submittedScore = parsedScoreResult.Value;

        int? replayFileId = null;

        if (replay is { Length: >= OsuReplayFileHeaderSize })
        {
            var replayFileResult = await database.Scores.Files.AddReplayFile(session.UserId, replay);

            if (replayFileResult.IsFailure)
            {
                Log.Error("Failed to save replay file for user {UserId}: {Error}", session.UserId, replayFileResult.Error);
                throw new Exception($"Failed to save replay for user {session.UserId}: {replayFileResult.Error}"); // Throw to make osu client retry the score submission
            }

            replayFileId = replayFileResult.Value.Id;
        }

        var timeElapsed = ScoreSubmissionUtil.GetTimeElapsed(submittedScore, scoreTime, scoreFailTime);

        var candidate = new ScoreSubmissionRequest
        {
            UserId = session.UserId,
            ScoreHash = submittedScore.ScoreHash,
            ScoreSerialized = scoreSerialized,
            BeatmapHash = beatmapHash,
            TimeElapsed = timeElapsed,
            OsuVersion = osuVersion,
            ClientHash = clientHash,
            ReplayFileId = replayFileId,
            StoryboardHash = storyboardHash,
            UserHash = session.Attributes.UserHash,
            WhenPlayed = scoreSubmittedAt
        };

        ScoreProcessingError? processingError = null;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Configuration.ScoreProcessingTimeoutSeconds));
            var processSubmissionResult = await submissionTaskHandler.ExecuteInlineSubmission(session, candidate, cts.Token);

            if (processSubmissionResult.IsSuccess)
                return processSubmissionResult.Value ?? "error: no";

            if (processSubmissionResult.Error.Code == ScoreProcessingErrorCode.DuplicateScore)
                return "error: no";

            processingError = processSubmissionResult.Error;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected exception during sync score submission for user {UserId}", session.UserId);
        }

        try
        {
            await EnqueueForBackgroundRetry(candidate, session, processingError);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enqueue score for background retry for user {UserId}", session.UserId);
        }

        return "error: no";
    }

    [TraceExecution]
    public async Task<string> GetBeatmapScores(Session session, int setId, GameMode gameMode, Mods mods,
        LeaderboardType leaderboardType, string beatmapHash, string filename, CancellationToken ct = default)
    {
        gameMode = gameMode.EnrichWithMods(mods);

        var user = await database.Users.GetUser(session.UserId, ct: ct);
        if (user == null)
            return $"{(int)BeatmapStatus.NotSubmitted}|false";

        var (databaseScores, _) = await database.Scores.GetBeatmapScores(beatmapHash,
            gameMode,
            leaderboardType,
            mods,
            user,
            new QueryOptions(true)
            {
                IgnoreCountQueryIfExists = true,
                QueryModifier = q => q.Cast<Score>().IncludeUser()
            },
            ct);

        var scores = databaseScores.WithLeaderboardPositions();
        var leaderboardPositions = scores.ToDictionary(entry => entry.Score.Id, entry => entry.LeaderboardPosition);

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, setId, beatmapHash, retryCount: int.MaxValue, ct: ct);

        if (beatmapSetResult.IsFailure)
        {
            return $"{(int)BeatmapStatus.NotSubmitted}|false";
        }

        var beatmapSet = beatmapSetResult.Value;

        var beatmap = beatmapSet?.Beatmaps?.FirstOrDefault(x => x.Checksum == beatmapHash);

        if (beatmapSet == null || beatmap == null)
            return $"{(int)BeatmapStatus.NotSubmitted}|false"; // TODO: Check with our db to find out if need's update

        if (beatmap.Status < BeatmapStatus.Ranked)
            return $"{(int)beatmap.Status}|false";

        var responses = new List<string>
        {
            $"{(int)beatmap.Status}|false|{beatmap.Id}|{beatmap.BeatmapsetId}|{scores.Count}",
            $"0\n{beatmapSet.Artist} - {beatmapSet.Title}\n10.0"
        };

        if (scores.Count == 0)
            return string.Join("\n", responses);

        var userPersonalBestScores = scores.Select(entry => entry.Score).ToList().GetUserPersonalBestScores(session.UserId);

        var personalBest = userPersonalBestScores?.BestScoreByScoreValue;
        responses.Add(personalBest != null
            ? personalBest.GetString(leaderboardPositions[personalBest.Id])
            : "");

        var leaderboardScores = scores.Select(entry => entry.Score).ToList().GetScoresGroupedByUsersBest().Take(50);

        foreach (var score in leaderboardScores)
        {
            responses.Add(score.GetString(leaderboardPositions[score.Id]));
        }

        return string.Join("\n", responses);
    }

    private async Task EnqueueForBackgroundRetry(ScoreSubmissionRequest candidate, Session userSession, ScoreProcessingError? error = null)
    {
        var shouldParkAsFailed = error is { Disposition: ScoreProcessingDisposition.Permanent }
                                 || error.HasValue && Configuration.ScoreProcessingMaxRetries <= 0;

        int? enqueuedTaskId = null;

        var enqueueResult = await database.CommitAsTransactionAsync(async () =>
        {
            await database.ScoreSubmissionRequests.AddQueueEntry(candidate);

            var task = new ScoreProcessingTask
            {
                TaskType = ScoreTaskType.Submission,
                ScoreSubmissionRequest = candidate,
                Priority = (int)ScoreProcessingPriority.High,
                CreatedAt = DateTime.UtcNow
            };

            if (shouldParkAsFailed && error.HasValue)
            {
                var processingError = error.Value;
                task.Status = ScoreProcessingStatus.Failed;
                task.ErrorCode = processingError.Code;
                task.ErrorMessage = processingError.Message;
            }

            await database.ScoreProcessingTasks.AddQueueEntry(task);
            enqueuedTaskId = task.Id;
        });

        if (enqueueResult.IsFailure)
            throw new Exception($"Failed to enqueue score for background retry: {enqueueResult.Error}");

        await database.Events.ScoreProcessing.AddSubmissionEnqueuedEvent(candidate.Id, candidate.UserId, enqueuedTaskId);

        if (!shouldParkAsFailed)
        {
            userSession.SendNotification("One of your recent scores seems to have trouble retrieving its beatmap data. This score may be missing from your profile or the leaderboards for now, but it will be fixed automatically once we can retrieve the beatmap data.");
        }
    }
}
