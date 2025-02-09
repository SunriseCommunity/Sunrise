using osu.Shared;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Utils;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Extensions;

public static class UserStatsExtensions
{
    public static async Task UpdateWithScore(this UserStats userStats, Score score, Score? prevScore, int timeElapsed)
    {
        var isNewScore = prevScore == null;
        var isBetterScore = !isNewScore && score.TotalScore > prevScore!.TotalScore;
        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        userStats.IncreaseTotalScore(score.TotalScore);
        userStats.IncreaseTotalHits(score);
        userStats.IncreasePlayTime(timeElapsed);
        userStats.IncreasePlaycount();

        if (isFailed || !score.IsScoreable)
            return;

        userStats.UpdateMaxCombo(score.MaxCombo);

        if ((isNewScore || isBetterScore) && score.LocalProperties.IsRanked)
        {
            // If new score, add it to the ranked score. If a better score, add the difference between the new and the previous score.
            userStats.RankedScore += isNewScore ? score.TotalScore : score.TotalScore - prevScore!.TotalScore;

            userStats.PerformancePoints = 
                await Calculators.CalculateUserWeightedPerformance(userStats.UserId, score.GameMode, score);
            userStats.Accuracy = await Calculators.CalculateUserWeightedAccuracy(userStats.UserId, score.GameMode, score);
        }
    }

    private static void IncreaseTotalHits(this UserStats userStats, Score newScore)
    {
        userStats.TotalHits += newScore.Count300 + newScore.Count100 + newScore.Count50;
        if ((GameMode)userStats.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania)
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