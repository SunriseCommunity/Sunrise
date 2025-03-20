using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Mods = osu.Shared.Mods;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Utils.Calculators;

public static class PerformanceCalculator
{
    public static double CalculateUserWeightedAccuracy(List<Score> userBestScores, Score? score = null)
    {
        if (userBestScores.Count == 0 && score == null) return 0;

        if (userBestScores.Count > 100) throw new ArgumentOutOfRangeException(nameof(userBestScores));

        if (score != null)
        {
            userBestScores = userBestScores.UpsertUserScoreToSortedScores(score).SortScoresByPerformancePoints();
        }

        var top100Scores = userBestScores.Take(100).ToList();

        var weightedAccuracy = top100Scores
            .Select((s, i) => Math.Pow(0.95, i) * s.Accuracy)
            .Sum();
        var bonusAccuracy = 100 / (20 * (1 - Math.Pow(0.95, top100Scores.Count)));

        return weightedAccuracy * bonusAccuracy / 100;
    }

    public static double CalculateUserWeightedPerformance(List<Score> userBestScores, Score? score = null)
    {
        if (userBestScores.Count == 0 && score == null) return 0;

        if (userBestScores.Count > 100) throw new ArgumentOutOfRangeException(nameof(userBestScores));

        if (score != null)
        {
            userBestScores = userBestScores.UpsertUserScoreToSortedScores(score).SortScoresByPerformancePoints();
        }

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
        var scoreVanillaGameMode = (GameMode)score.GameMode.ToVanillaGameMode();

        var totalHits = scoreVanillaGameMode switch
        {
            GameMode.Standard => score.Count300 + score.Count100 + score.Count50 + score.CountMiss,
            GameMode.Taiko => score.Count300 + score.Count100 + score.CountMiss,
            GameMode.CatchTheBeat => score.Count300 + score.Count100 + score.Count50 + score.CountKatu + score.CountMiss,
            GameMode.Mania => score.Count300 + score.Count100 + score.Count50 + score.CountGeki + score.CountKatu + score.CountMiss,
            _ => 0
        };

        if (totalHits == 0) return 0;

        return scoreVanillaGameMode switch
        {
            GameMode.Standard => 100f * (score.Count300 * 300f + score.Count100 * 100f + score.Count50 * 50f) / (totalHits * 300f),
            GameMode.Taiko => 100f * (score.Count300 + score.Count100 * 0.5f) / totalHits,
            GameMode.CatchTheBeat => 100f * (score.Count300 + score.Count100 + score.Count50) / totalHits,
            GameMode.Mania => score.Mods.HasFlag(Mods.ScoreV2) switch
            {
                true => 100f * (score.CountGeki * 305f + score.Count300 * 300f + score.CountKatu * 200f + score.Count100 * 100f + score.Count50 * 50f) / (totalHits * 305f),
                false => 100f * ((score.Count300 + score.CountGeki) * 300f + score.CountKatu * 200f + score.Count100 * 100f + score.Count50 * 50f) / (totalHits * 300f)
            },
            _ => 0
        };
    }
}