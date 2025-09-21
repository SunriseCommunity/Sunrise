using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Shared.Extensions.Users;

public static class UserStatsExtensions
{
    public static async Task UpdateWithScore(this UserStats userStats, Score score, UserPersonalBestScores? personalBestScores, int timeElapsed)
    {
        var isNewScore = personalBestScores == null;
        var isBetterTotalScoreValue = !isNewScore && score.TotalScore > personalBestScores?.BestScoreBasedByTotalScore.TotalScore;
        var isBetterPerformanceValue = Configuration.UseNewPerformanceCalculationAlgorithm
            ? !isNewScore && score.PerformancePoints > personalBestScores?.BestScoreForPerformanceCalculation.PerformancePoints
            : isBetterTotalScoreValue;
        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        userStats.IncreaseTotalScore(score.TotalScore);
        userStats.IncreaseTotalHits(score);
        userStats.IncreasePlayTime(timeElapsed);
        userStats.IncreasePlaycount();

        if (isFailed || !score.IsScoreable)
            return;

        userStats.UpdateMaxCombo(score.MaxCombo);

        if ((isNewScore || isBetterTotalScoreValue) && score.LocalProperties.IsRanked)
        {
            // If new score, add it to the ranked score. If a better score, add the difference between the new and the previous score.
            userStats.RankedScore += isNewScore ? score.TotalScore : score.TotalScore - personalBestScores!.BestScoreBasedByTotalScore.TotalScore;
        }

        if ((isNewScore || isBetterPerformanceValue) && score.LocalProperties.IsRanked)
        {
            using var scope = ServicesProviderHolder.CreateScope();
            var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

            userStats.PerformancePoints =
                await calculatorService.CalculateUserWeightedPerformance(userStats.UserId, score.GameMode, score);
            userStats.Accuracy = await calculatorService.CalculateUserWeightedAccuracy(userStats.UserId, score.GameMode, score);
        }
    }

    public static void UpdateWithDbScore(this UserStats userStats, Score score)
    {
        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        userStats.IncreaseTotalScore(score.TotalScore);
        userStats.IncreaseTotalHits(score);
        userStats.IncreasePlaycount();

        if (isFailed || !score.IsScoreable)
            return;

        userStats.UpdateMaxCombo(score.MaxCombo);

        if (score.SubmissionStatus == SubmissionStatus.Best && score.BeatmapStatus.IsRanked())
        {
            userStats.RankedScore += score.TotalScore;
        }
    }

    private static void IncreaseTotalHits(this UserStats userStats, Score newScore)
    {
        userStats.TotalHits += newScore.Count300 + newScore.Count100 + newScore.Count50;
        if ((GameMode)newScore.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania)
            userStats.TotalHits += newScore.CountGeki + newScore.CountKatu;
    }

    private static void UpdateMaxCombo(this UserStats userStats, int combo)
    {
        userStats.MaxCombo = Math.Max(userStats.MaxCombo, combo);
    }

    private static void IncreasePlayTime(this UserStats userStats, int time)
    {
        userStats.PlayTime += time;
    }

    private static void IncreaseTotalScore(this UserStats userStats, long score)
    {
        userStats.TotalScore += score;
    }

    private static void IncreasePlaycount(this UserStats userStats)
    {
        userStats.PlayCount++;
    }
}