using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects;
using Mods = osu.Shared.Mods;
using GameModeVanilla = osu.Shared.GameMode;

namespace Sunrise.Shared.Utils.Calculators;

public static class PerformanceCalculator
{
    public static double CalculateUserWeightedAccuracy(List<Score> userBestScores)
    {
        if (userBestScores.Count == 0) return 0;

        if (userBestScores.Count > 100) throw new ArgumentOutOfRangeException(nameof(userBestScores));

        var top100Scores = userBestScores.Take(100).ToList();

        var weightedAccuracy = top100Scores
            .Select((s, i) => Math.Pow(0.95, i) * s.Accuracy)
            .Sum();
        var bonusAccuracy = 100 / (20 * (1 - Math.Pow(0.95, top100Scores.Count)));

        return weightedAccuracy * bonusAccuracy / 100;
    }

    public static double CalculateUserWeightedPerformance(List<Score> userBestScores)
    {
        if (userBestScores.Count == 0) return 0;

        if (userBestScores.Count > 100) throw new ArgumentOutOfRangeException(nameof(userBestScores));

        var top100Scores = userBestScores.Take(100).ToList();

        const double bonusNumber = 416.6667;
        var weightedPp = top100Scores
            .Select((s, i) => Math.Pow(0.95, i) * s.PerformancePoints)
            .Sum();
        var bonusPp = bonusNumber * (1 - Math.Pow(0.9994, top100Scores.Count));

        return weightedPp + bonusPp;
    }

    public static float CalculateAccuracy(Score score)
    {
        return CalculateAccuracy(score.Count300, score.Count100, score.Count50, score.CountMiss, score.CountKatu, score.CountGeki, score.GameMode.ToVanillaGameMode(), score.Mods);
    }

    public static float CalculateAccuracy(SubmittedScore score)
    {
        return CalculateAccuracy(score.Count300, score.Count100, score.Count50, score.CountMiss, score.CountKatu, score.CountGeki, score.GameMode.ToVanillaGameMode(), score.Mods);
    }

    private static float CalculateAccuracy(
        int count300,
        int count100,
        int count50,
        int countMiss,
        int countKatu,
        int countGeki,
        GameModeVanilla mode,
        Mods mods)
    {
        var totalHits = mode switch
        {
            GameModeVanilla.Standard => count300 + count100 + count50 + countMiss,
            GameModeVanilla.Taiko => count300 + count100 + countMiss,
            GameModeVanilla.CatchTheBeat => count300 + count100 + count50 + countKatu + countMiss,
            GameModeVanilla.Mania => count300 + count100 + count50 + countGeki + countKatu + countMiss,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        if (totalHits == 0) return 0;

        return mode switch
        {
            GameModeVanilla.Standard => 100f * (count300 * 300f + count100 * 100f + count50 * 50f) / (totalHits * 300f),
            GameModeVanilla.Taiko => 100f * (count300 + count100 * 0.5f) / totalHits,
            GameModeVanilla.CatchTheBeat => 100f * (count300 + count100 + count50) / totalHits,
            GameModeVanilla.Mania => mods.HasFlag(Mods.ScoreV2) switch
            {
                true => 100f * (countGeki * 305f + count300 * 300f + countKatu * 200f + count100 * 100f + count50 * 50f) / (totalHits * 305f),
                false => 100f * ((count300 + countGeki) * 300f + countKatu * 200f + count100 * 100f + count50 * 50f) / (totalHits * 300f)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }
}