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

                    var ignoreNotScoredMods = mods & ~Mods.Relax;
                    performance.IMods((uint)ignoreNotScoredMods);
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