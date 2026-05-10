using Serilog;
using Sunrise.API.Enums;
using Sunrise.API.Objects;
using Sunrise.API.Serializable.Response;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using WebSocketManager = Sunrise.API.Managers.WebSocketManager;

namespace Sunrise.Processing.Services;

[TraceExecution]
public class ScoreSideEffectsPublisherService(
    DatabaseService database,
    CalculatorService calculatorService,
    MedalService medalService,
    WebSocketManager webSocketManager,
    SessionRepository sessions,
    ChatChannelRepository channels)
{
    public async Task<string> PublishScoreSideEffectsAndBuildSubmissionResponse(
        BaseSession beatmapRatelimitSession,
        ScoreCommitContext ctx,
        UserStats prevUserStats,
        CancellationToken ct = default)
    {
        if (ctx.Beatmap == null || ctx.BeatmapSet == null)
            throw new InvalidOperationException("Cannot publish side effects without beatmap and beatmap set on context.");

        await PublishScoreSideEffects(beatmapRatelimitSession, ctx.Score, ctx.BeatmapSet, ctx.Beatmap, ctx.User, ctx.UserStats, ct);

        var newAchievements = await UnlockMedalsAndGetNewlyUnlocked(ctx.Score, ctx.Beatmap, ctx.UserStats);

        return ScoreSubmissionUtil.GetScoreSubmitResponse(ctx.Beatmap, ctx.UserStats, prevUserStats, ctx.Score, ctx.UserPersonalBestScores?.OverallPeer, newAchievements);
    }

    private async Task PublishScoreSideEffects(
        BaseSession beatmapRatelimitSession,
        Score score,
        BeatmapSet beatmapSet,
        Beatmap beatmap,
        User user,
        UserStats userStats,
        CancellationToken ct = default)
    {
        SunriseMetrics.ScoreSubmittedCounterInc(score.UserId, beatmap.Id, score.GameMode, score.Mods, score.PerformancePoints, score.Id);

        webSocketManager.BroadcastJsonAsync(new WebSocketMessage(WebSocketEventType.NewScoreSubmitted, new ScoreResponse(sessions, score)));

        if ((int)score.GameMode != beatmap.ModeInt || (int)score.Mods > 0)
        {
            var recalculateBeatmapResult =
                await calculatorService.CalculateBeatmapPerformance(beatmapRatelimitSession, score.BeatmapId, score.GameMode, score.Mods);

            if (recalculateBeatmapResult.IsFailure)
            {
                Log.Warning("Failed to recalculate beatmap performance for beatmap {BeatmapId} with mods {Mods} after score submission: {Error}",
                    beatmap.Id,
                    score.Mods,
                    recalculateBeatmapResult.Error);
            }

            beatmap.UpdateBeatmapWithPerformance(score.Mods, recalculateBeatmapResult.Value);
        }

        var (globalScores, _) = await database.Scores.GetBeatmapScores(
            score.BeatmapHash,
            score.GameMode,
            options: new QueryOptions
            {
                AsNoTracking = true,
                IgnoreCountQueryIfExists = true
            },
            ct: ct);

        var previousLeaderboardTopUserId = globalScores
            .Where(s => s.ScoreHash != score.ScoreHash)
            .ToList()
            .SortScoresByTheirScoreValue()
            .FirstOrDefault()
            ?.UserId;

        globalScores = globalScores.UpsertUserScoreToSortedScores(score);
        score = globalScores.First(s => s.ScoreHash == score.ScoreHash);

        var (newUserRank, _) = await database.Users.Stats.Ranks.GetUserRanks(user, userStats.GameMode, ct: ct);
        userStats.LocalProperties.Rank = newUserRank;

        var shouldAnnounceNewFirstPlace = score.LocalProperties.LeaderboardPosition == 1
                                          && previousLeaderboardTopUserId.HasValue
                                          && previousLeaderboardTopUserId.Value != score.UserId;

        if (shouldAnnounceNewFirstPlace)
        {
            channels.GetScoreAnnouncementChannel()
                ?.SendToChannel(ScoreSubmissionUtil.GetNewFirstPlaceString(score, beatmapSet, beatmap));
        }
    }

    private async Task<string> UnlockMedalsAndGetNewlyUnlocked(Score score, Beatmap beatmap, UserStats userStats)
    {
        return await medalService.UnlockAndGetNewMedals(score, beatmap, userStats);
    }
}