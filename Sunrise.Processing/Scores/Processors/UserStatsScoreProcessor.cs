using osu.Shared;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Processors;

[TraceExecution]
public class UserStatsScoreProcessor(
    DatabaseService database,
    CalculatorService calculatorService) : ScoreEntityProcessorBase
{
    public override int Priority => 200;

    protected override async Task OnNewSubmissionInternal(ScoreCommitContext ctx)
    {
        await IncrementUserStats(ctx);
    }

    protected override async Task OnRecalculationInternal(ScoreCommitContext ctx)
    {
        await ApplyWeightedRefresh(ctx);
    }

    protected override async Task OnDeletionInternal(ScoreCommitContext ctx)
    {
        await DecrementUserStats(ctx);
    }

    protected override async Task OnRestorationInternal(ScoreCommitContext ctx)
    {
        await IncrementUserStats(ctx);
    }

    protected override async Task AfterExecution(ScoreCommitContext ctx)
    {
        var updateUserStatsResult = await database.Users.Stats.UpdateUserStats(ctx.UserStats, ctx.User);
        if (updateUserStatsResult.IsFailure)
            throw new ApplicationException("Failed to persist user stats: " + updateUserStatsResult.Error);
    }

    private async Task IncrementUserStats(ScoreCommitContext ctx)
    {
        var score = ctx.Score;
        var userStats = ctx.UserStats;
        var personalBestScores = ctx.UserPersonalBestScores?.OverallPeer;

        var isFirstBeatmapScore = personalBestScores == null;

        var isBetterTotalScoreValue = isFirstBeatmapScore || score.TotalScore > personalBestScores?.BestScoreBasedByTotalScore.TotalScore;
        var isBetterPerformanceValue = isFirstBeatmapScore || (
            Configuration.UseNewPerformanceCalculationAlgorithm
                ? score.PerformancePoints > personalBestScores?.BestScoreForPerformanceCalculation.PerformancePoints
                : isBetterTotalScoreValue);

        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        userStats.TotalScore += score.TotalScore;
        IncreaseTotalHits(userStats, score);
        userStats.PlayTime += score.TimeElapsed;
        userStats.PlayCount++;

        if (isFailed || !score.IsScoreable)
            return;

        userStats.MaxCombo = Math.Max(userStats.MaxCombo, score.MaxCombo);

        if (isBetterTotalScoreValue && score.LocalProperties.IsRanked)
        {
            userStats.RankedScore += isFirstBeatmapScore
                ? score.TotalScore
                : score.TotalScore - personalBestScores!.BestScoreBasedByTotalScore.TotalScore;
        }

        if (isBetterPerformanceValue && score.LocalProperties.IsRanked)
        {
            (userStats.PerformancePoints, userStats.Accuracy) = await calculatorService.CalculateUserWeightedStats(ctx.User, score.GameMode);
        }
    }

    private async Task DecrementUserStats(ScoreCommitContext ctx)
    {
        var score = ctx.Score;
        var userStats = ctx.UserStats;
        var original = ctx.OriginalState;

        var isFailed = !original.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        userStats.TotalScore = Math.Max(0, userStats.TotalScore - score.TotalScore);
        DecreaseTotalHits(userStats, score);
        userStats.PlayTime = Math.Max(0, userStats.PlayTime - score.TimeElapsed);
        userStats.PlayCount = Math.Max(0, userStats.PlayCount - 1);

        if (isFailed || !original.IsScoreable)
            return;

        if (score.MaxCombo == userStats.MaxCombo)
        {
            var fallbackMax = await database.Scores.GetUserMaxComboExcluding(score.UserId, score.GameMode, score.Id);
            if (fallbackMax.HasValue && fallbackMax.Value < userStats.MaxCombo)
                userStats.MaxCombo = fallbackMax.Value;
        }

        if (original is { SubmissionStatus: SubmissionStatus.Best, IsRanked: true })
        {
            var promotedPeer = ctx.UserPersonalBestScores?.SameModsPeer?.BestScoreBasedByTotalScore;
            var rankedDecrement = promotedPeer != null
                ? score.TotalScore - promotedPeer.TotalScore
                : score.TotalScore;

            userStats.RankedScore = Math.Max(0, userStats.RankedScore - rankedDecrement);
        }

        if (!original.IsRanked)
            return;

        (userStats.PerformancePoints, userStats.Accuracy) = await calculatorService.CalculateUserWeightedStats(ctx.User, score.GameMode);
    }

    private async Task ApplyWeightedRefresh(ScoreCommitContext ctx)
    {
        var score = ctx.Score;
        if (!score.LocalProperties.IsRanked || !score.IsScoreable || !score.IsPassed)
            return;

        (ctx.UserStats.PerformancePoints, ctx.UserStats.Accuracy) = await calculatorService.CalculateUserWeightedStats(ctx.User, score.GameMode);
    }

    private static void IncreaseTotalHits(UserStats userStats, Score score)
    {
        userStats.TotalHits += score.Count300 + score.Count100 + score.Count50;
        if ((GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania)
            userStats.TotalHits += score.CountGeki + score.CountKatu;
    }

    private static void DecreaseTotalHits(UserStats userStats, Score score)
    {
        var delta = score.Count300 + score.Count100 + score.Count50;
        if ((GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania)
            delta += score.CountGeki + score.CountKatu;

        userStats.TotalHits = Math.Max(0, userStats.TotalHits - delta);
    }
}