using System.Net;
using osu.Shared;
using Sunrise.API.Enums;
using Sunrise.API.Objects;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Services.Helpers.Scores;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using WebSocketManager = Sunrise.API.Managers.WebSocketManager;

namespace Sunrise.Server.Services;

public class ScoreService(BeatmapService beatmapService, DatabaseService database, CalculatorService calculatorService, MedalService medalService, WebSocketManager webSocketManager, SessionRepository sessions)
{
    public async Task<string> SubmitScore(Session session, string scoreSerialized, string beatmapHash,
        int scoreTime, int scoreFailTime, string osuVersion, string clientHash, IFormFile? replay,
        string? storyboardHash)
    {
        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapHash: beatmapHash, retryCount: int.MaxValue);

        if (beatmapSetResult.IsFailure)
        {
            var isBeatmapsetNotFound = beatmapSetResult.Error.Status == HttpStatusCode.NotFound;

            SubmitScoreHelper.ReportRejectionToMetrics(session,
                scoreSerialized,
                isBeatmapsetNotFound ? "Invalid request: BeatmapSet not found" : "Beatmap set couldn't be retrieved due to ratelimit timeout, please report this to the developer.");

            return "error: no";
        }

        var beatmapSet = beatmapSetResult.Value;

        var beatmap = beatmapSet?.Beatmaps.FirstOrDefault(x => x.Checksum == beatmapHash);

        if (beatmap == null || beatmapSet == null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Invalid request: BeatmapSet not found");
            return "error: no";
        }

        var score = scoreSerialized.TryParseToSubmittedScore(session, beatmap);
        var dbScore = await database.Scores.GetScore(score.ScoreHash);

        if (dbScore != null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Score with same hash already exists");
            return "error: no";
        }

        if (replay is { Length: >= 24 })
        {
            var replayFileResult = await database.Scores.Files.AddReplayFile(session.UserId, replay);

            if (replayFileResult.IsFailure)
            {
                await SaveRejectedScore(score);
                SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, $"Couldn't add replay file for score, reason: {replayFileResult.Error}");
                return "error: no";
            }

            score.ReplayFileId = replayFileResult.Value.Id;
        }

        var isCurrentScoreFailed = SubmitScoreHelper.IsScoreFailed(score);

        if (!isCurrentScoreFailed && score.ReplayFileId == null)
        {
            await SaveRejectedScore(score);
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Replay file not found for passed score");
            return "error: no";
        }

        if (SubmitScoreHelper.IsHasInvalidMods(score.Mods))
        {
            await SaveRejectedScore(score);
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Invalid mods");
            return "error: no";
        }

        var notStandardMods = score.Mods.TryGetSelectedNotStandardMods();

        var hasNonStandardMods = notStandardMods is not Mods.None;
        var isHasMoreThanOneNotStandardMod = !notStandardMods.IsSingleMod() && hasNonStandardMods;
        var isNonSupportedNonStandardMod = (int)score.GameMode < 4 && hasNonStandardMods;

        // Disallow submitting scores with double not standard mods (e.g. ScoreV2 + Relax) or with which we are not supporting (e.g. shouldn't exist)
        if (isHasMoreThanOneNotStandardMod || isNonSupportedNonStandardMod)
        {
            await SaveRejectedScore(score);
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Includes non-standard mod(s), which is not supported for this game mode");
            return "error: no";
        }

        // Auto-restrict players who submit scores with too many performance points on standard game modes
        if (score.PerformancePoints >= Configuration.BannablePpThreshold && !hasNonStandardMods && score.LocalProperties.IsRanked)
        {
            await SaveRejectedScore(score);
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Too many performance points. Cheating?");
            await database.Users.Moderation.RestrictPlayer(session.UserId, null, "Auto-restricted for submitting impossible score");
            return "error: no";
        }

        var isScoreValid = SubmitScoreHelper.IsScoreValid(session,
            score,
            clientHash,
            beatmapHash,
            beatmap.Checksum,
            storyboardHash);

        if (!isScoreValid)
        {
            await SaveRejectedScore(score);
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Invalid checksums");
            await database.Users.Moderation.RestrictPlayer(session.UserId, null, "Invalid checksums on score submission");
            return "error: no";
        }

        var isScoreScoreable = !isCurrentScoreFailed && score.IsScoreable;

        var (databaseScores, _) = isScoreScoreable
            ? await database.Scores.GetBeatmapScores(score.BeatmapHash,
                score.GameMode,
                options: new QueryOptions
                {
                    IgnoreCountQueryIfExists = true
                })
            : ([], 0);

        var globalScores = databaseScores.EnrichWithLeaderboardPositions();

        var (databaseScoresWithSameMods, _) = isScoreScoreable
            ? await database.Scores.GetBeatmapScores(score.BeatmapHash,
                score.GameMode,
                LeaderboardType.GlobalWithMods,
                score.Mods,
                options: new QueryOptions
                {
                    IgnoreCountQueryIfExists = true
                })
            : ([], 0);

        var scoresWithSameMods = databaseScoresWithSameMods.EnrichWithLeaderboardPositions();

        var userStats = await database.Users.Stats.GetUserStats(score.UserId, score.GameMode);

        if (userStats == null)
        {
            await SaveRejectedScore(score);
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "User stats not found");
            return "error: no";
        }

        var prevUserStats = userStats.Clone();
        var prevPBest = globalScores.GetPersonalBestOf(score.UserId);

        var user = await database.Users.GetUser(session.UserId);

        if (user == null)
        {
            await SaveRejectedScore(score);
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Couldn't find user while submitting score");
            return "error: no";
        }

        var (prevUserGlobalRank, _) = isScoreScoreable ? await database.Users.Stats.Ranks.GetUserRanks(user, userStats.GameMode) : (0, 0);
        prevUserStats.LocalProperties.Rank = prevUserGlobalRank;

        var timeElapsed = SubmitScoreHelper.GetTimeElapsed(score, scoreTime, scoreFailTime);

        var userGrades = await database.Users.Grades.GetUserGrades(user.Id, userStats.GameMode);

        if (userGrades == null)
        {
            await SaveRejectedScore(score);
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Couldn't find user grades while submitting score");
            return "error: no";
        }

        var transactionResult = await database.CommitAsTransactionAsync(async () =>
        {
            var prevPBestWithSameMods = scoresWithSameMods.GetPersonalBestOf(score.UserId);
            score.UpdateSubmissionStatus(prevPBestWithSameMods);

            await userStats.UpdateWithScore(score, prevPBest, timeElapsed);
            userGrades.UpdateWithScore(score, prevPBest);

            if (prevPBestWithSameMods != null && score.SubmissionStatus == SubmissionStatus.Best)
            {
                // Best score shouldn't be failed, but adding this check just in case
                prevPBestWithSameMods.SubmissionStatus = prevPBestWithSameMods.IsPassed ? SubmissionStatus.Submitted : SubmissionStatus.Failed;
                await database.Scores.UpdateScore(prevPBestWithSameMods);
            }

            await database.Scores.AddScore(score);
            await database.Users.Stats.UpdateUserStats(userStats, user);
            await database.Users.Grades.UpdateUserGrades(userGrades);
        });

        if (transactionResult.IsFailure)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Failed to execute transaction for score submission");
            return "error: no";
        }

        if (!isScoreScoreable)
        {
            return "error: no"; // No need to create chart/unlock medals for failed or for scores that are not scoreable
        }

        webSocketManager.BroadcastJsonAsync(new WebSocketMessage(WebSocketEventType.NewScoreSubmitted, new ScoreResponse(sessions, score)));

        // Mods can change difficulty rating, important to recalculate it for right medal unlocking
        if ((int)score.GameMode != beatmap.ModeInt || (int)score.Mods > 0)
        {
            var recalculateBeatmapResult = await calculatorService.CalculateBeatmapPerformance(session, score.BeatmapId, score.GameMode, score.Mods);

            if (recalculateBeatmapResult.IsFailure)
            {
                SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuSubmitScore, session, recalculateBeatmapResult.Error.Message);
            }
            else
            {
                beatmap.UpdateBeatmapWithPerformance(score.Mods, recalculateBeatmapResult.Value);
            }
        }

        var updatedScores = globalScores.UpsertUserScoreToSortedScores(score);
        var newPBest = updatedScores.GetPersonalBestOf(score.UserId) ?? score;

        var (newUserRank, _) = await database.Users.Stats.Ranks.GetUserRanks(user, userStats.GameMode);
        userStats.LocalProperties.Rank = newUserRank;

        if (newPBest.LocalProperties.LeaderboardPosition == 1 && globalScores.Count > 0 && globalScores[0].UserId != score.UserId)
        {
            var channels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();
            channels.GetChannel(session, "#announce")
                ?.SendToChannel(SubmitScoreHelper.GetNewFirstPlaceString(session, newPBest, beatmapSet, beatmap));
        }

        var newAchievements = await medalService.UnlockAndGetNewMedals(newPBest, beatmap, userStats);

        return await SubmitScoreHelper.GetScoreSubmitResponse(beatmap, userStats, prevUserStats, newPBest, prevPBest, newAchievements);
    }

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

        var scores = databaseScores.EnrichWithLeaderboardPositions();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, setId, beatmapHash, retryCount: int.MaxValue, ct: ct);

        if (beatmapSetResult.IsFailure)
        {
            return $"{(int)BeatmapStatus.NotSubmitted}|false";
        }

        var beatmapSet = beatmapSetResult.Value;

        var beatmap = beatmapSet?.Beatmaps.FirstOrDefault(x => x.Checksum == beatmapHash);

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

        var personalBest = scores.GetPersonalBestOf(session.UserId);
        responses.Add(personalBest != null ? personalBest.GetString() : "");

        var leaderboardScores = scores.GetScoresGroupedByUsersBest().Take(50);

        foreach (var score in leaderboardScores)
        {
            responses.Add(score.GetString());
        }

        return string.Join("\n", responses);
    }

    private async Task SaveRejectedScore(Score score)
    {
        score.SubmissionStatus = SubmissionStatus.Deleted;
        await database.Scores.AddScore(score);
    }
}