using System.Runtime.InteropServices;
using RosuPP;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Beatmap = RosuPP.Beatmap;
using Mods = osu.Shared.Mods;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Utils.Calculators;

public static class PerformanceCalculator
{
    public static double CalculatePerformancePoints(byte[] beatmapBytes, Score score)
    {
        return UsingAllocatedBeatmapHandle(handle =>
            {
                var bytesPointer = new Sliceu8(handle, (uint)beatmapBytes.Length);
                using var beatmap = Beatmap.FromBytes(bytesPointer);

                beatmap.Convert((Mode)score.GameMode.ToVanillaGameMode());

                using var performance = GetUserPerformance(score);
                var result = performance.Calculate(beatmap.Context);

                return result.mode switch
                {
                    Mode.Osu => result.osu.ToNullable()!.Value.pp,
                    Mode.Taiko => result.taiko.ToNullable()!.Value.pp,
                    Mode.Catch => result.fruit.ToNullable()!.Value.pp,
                    Mode.Mania => result.mania.ToNullable()!.Value.pp,
                    _ => 0
                };
            },
            beatmapBytes);
    }

    public static double RecalculateBeatmapDifficulty(byte[] beatmapBytes, int mode, Mods mods = Mods.None)
    {
        return UsingAllocatedBeatmapHandle(handle =>
            {
                var bytesPointer = new Sliceu8(handle, (uint)beatmapBytes.Length);
                using var beatmap = Beatmap.FromBytes(bytesPointer);

                var scoreVanillaGameMode = mode % 4;
                beatmap.Convert((Mode)scoreVanillaGameMode);

                using var difficulty = Difficulty.New();
                difficulty.IMods((uint)mods);
                var result = difficulty.Calculate(beatmap.Context);

                return result.mode switch
                {
                    Mode.Osu => result.osu.ToNullable()!.Value.stars,
                    Mode.Taiko => result.taiko.ToNullable()!.Value.stars,
                    Mode.Catch => result.fruit.ToNullable()!.Value.stars,
                    Mode.Mania => result.mania.ToNullable()!.Value.stars,
                    _ => -1
                };
            },
            beatmapBytes);
    }

    public static (double, double, double, double) CalculatePerformancePoints(byte[] beatmapBytes, int mode, Mods mods = Mods.None)
    {
        return UsingAllocatedBeatmapHandle(handle =>
            {
                var bytesPointer = new Sliceu8(handle, (uint)beatmapBytes.Length);
                using var beatmap = Beatmap.FromBytes(bytesPointer);

                var scoreVanillaGameMode = mode % 4;
                beatmap.Convert((Mode)scoreVanillaGameMode);

                var ppList = new List<double>();

                var accuracyCalculate = new List<double>
                {
                    100,
                    99,
                    98,
                    95
                };

                foreach (var accuracy in accuracyCalculate)
                {
                    using var performance = Performance.New();
                    performance.Accuracy((uint)accuracy);
                    performance.IMods((uint)mods);
                    var result = performance.Calculate(beatmap.Context);
                    ppList.Add(result.mode switch
                    {
                        Mode.Osu => result.osu.ToNullable()!.Value.pp,
                        Mode.Taiko => result.taiko.ToNullable()!.Value.pp,
                        Mode.Catch => result.fruit.ToNullable()!.Value.pp,
                        Mode.Mania => result.mania.ToNullable()!.Value.pp,
                        _ => 0
                    });
                }

                return (ppList[0], ppList[1], ppList[2], ppList[3]);
            },
            beatmapBytes);
    }

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
        var bonusAccuracy = 100 / (20 * (1 - Math.Pow(0.95, userBestScores.Count)));

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
        var bonusPp = bonusNumber * (1 - Math.Pow(0.9994, userBestScores.Count));

        return weightedPp + bonusPp;
    }

    public static float CalculateAccuracy(Score score)
    {
        var totalHits = score.Count300 + score.Count100 + score.Count50 + score.CountMiss;

        var scoreVanillaGameMode = (GameMode)score.GameMode.ToVanillaGameMode();
        if (scoreVanillaGameMode == GameMode.Mania) totalHits += score.CountGeki + score.CountKatu;

        if (totalHits == 0) return 0;

        return scoreVanillaGameMode switch
        {
            GameMode.Standard => (float)(score.Count300 * 300 + score.Count100 * 100 + score.Count50 * 50) /
                (totalHits * 300) * 100,
            GameMode.Taiko => (float)(score.Count300 * 300 + score.Count100 * 150) / (totalHits * 300) * 100,
            GameMode.CatchTheBeat => (float)(score.Count300 + score.Count100 + score.Count50) / totalHits * 100,
            GameMode.Mania => (float)((score.Count300 + score.CountGeki) * 300 + score.CountKatu * 200 +
                                      score.Count100 * 100 + score.Count50 * 50) / (totalHits * 300) * 100,
            _ => 0
        };
    }

    private static Performance GetUserPerformance(Score score)
    {
        // Ignore Relax mod for more enhanced calculation for Relax
        var mods = score.Mods & ~Mods.Relax;

        var performance = Performance.New();
        performance.Accuracy((uint)score.Accuracy);
        performance.Combo((uint)score.MaxCombo);
        performance.N300((uint)score.Count300);
        performance.N100((uint)score.Count100);
        performance.N50((uint)score.Count50);
        performance.Misses((uint)score.CountMiss);
        performance.IMods((uint)mods);
        return performance;
    }

    private static T UsingAllocatedBeatmapHandle<T>(Func<GCHandle, T> action, byte[] beatmapBytes)
    {
        var handle = GCHandle.Alloc(beatmapBytes, GCHandleType.Pinned);

        try
        {
            return action(handle);
        }
        finally
        {
            handle.Free();
        }
    }
}