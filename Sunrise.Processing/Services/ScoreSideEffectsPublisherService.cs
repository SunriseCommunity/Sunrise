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

        await PublishScoreSideEffects(beatmapRatelimitSession, ctx, ct);

        var newAchievements = await UnlockMedalsAndGetNewlyUnlocked(ctx.Score, ctx.Beatmap, ctx.UserStats);

        var (newUserRank, _) = await database.Users.Stats.Ranks.GetUserRanks(ctx.User, ctx.UserStats.GameMode, ct: ct);
        ctx.UserStats.LocalProperties.Rank = newUserRank;

        var scoresWithLeaderboardPosition = await database.Scores.EnrichScoresWithLeaderboardPosition(new List<Score?>
            {
                ctx.Score,
                ctx.UserPersonalBestScores?.OverallPeer?.BestScoreBasedByTotalScore,
                ctx.UserPersonalBestScores?.OverallPeer?.BestScoreForPerformanceCalculation
            }.Where(s => s != null).Cast<Score>().ToList(),
            ct);

        // Fill leaderboard position for the graphs
        scoresWithLeaderboardPosition.ForEach(s =>
        {
            if (s.Id == ctx.Score.Id)
                ctx.Score.LocalProperties.LeaderboardPosition = s.LocalProperties.LeaderboardPosition;
            else if (ctx.UserPersonalBestScores?.OverallPeer != null)
            {
                if (s.Id == ctx.UserPersonalBestScores.OverallPeer.BestScoreBasedByTotalScore.Id)
                    ctx.UserPersonalBestScores.OverallPeer.BestScoreBasedByTotalScore.LocalProperties.LeaderboardPosition = s.LocalProperties.LeaderboardPosition;
                else if (s.Id == ctx.UserPersonalBestScores.OverallPeer.BestScoreForPerformanceCalculation.Id)
                    ctx.UserPersonalBestScores.OverallPeer.BestScoreForPerformanceCalculation.LocalProperties.LeaderboardPosition = s.LocalProperties.LeaderboardPosition;
            }
        });

        return ScoreSubmissionUtil.GetScoreSubmitResponse(ctx.Beatmap, ctx.UserStats, prevUserStats, ctx.Score, ctx.UserPersonalBestScores?.OverallPeer, newAchievements);
    }

    private async Task PublishScoreSideEffects(
        BaseSession beatmapRatelimitSession,
        ScoreCommitContext ctx,
        CancellationToken ct = default)
    {
        var score = ctx.Score;
        var beatmap = ctx.Beatmap;
        var beatmapSet = ctx.BeatmapSet;

        if (beatmap == null || beatmapSet == null)
            throw new InvalidOperationException("Beatmap and beatmap set must be present in context to publish score side effects.");

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
            options: new QueryOptions(new Pagination(1, 2))
            {
                AsNoTracking = true,
                IgnoreCountQueryIfExists = true
            },
            ct: ct);

        var isScoreFirstPlace = globalScores.FirstOrDefault()?.ScoreHash == score.ScoreHash;

        var secondBeatmapsBestFromDifferentUser = globalScores.Find(s => s.UserId != score.UserId);

        // TODO: Is checking by BestScoreBasedByTotalScore correct here?
        var isPeerWasFirstPlace = IsOverallBestScore(ctx.UserPersonalBestScores?.OverallPeer?.BestScoreBasedByTotalScore, secondBeatmapsBestFromDifferentUser);

        var shouldAnnounceNewFirstPlace = isScoreFirstPlace && !isPeerWasFirstPlace;

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

    private static bool IsOverallBestScore(Score? scoreA, Score? scoreB)
    {
        if (scoreB == null)
            return true;

        if (scoreA == null)
            return false;

        return new List<Score>
            {
                scoreA,
                scoreB
            }
            .SortScoresByTheirScoreValue()
            .First() == scoreA;
    }
}