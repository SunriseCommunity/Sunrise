using System.Runtime.InteropServices;
using RosuPP;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Extensions;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Enums;
using Beatmap = RosuPP.Beatmap;
using Mods = osu.Shared.Mods;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Utils;

public static class Calculators
{
    public static double CalculatePerformancePoints(Session session, Score score)
    {
        var beatmapBytes = BeatmapManager.GetBeatmapFile(session, score.BeatmapId).Result;

        if (beatmapBytes == null) return 0;

        var bytesPointer = new Sliceu8(GCHandle.Alloc(beatmapBytes, GCHandleType.Pinned), (uint)beatmapBytes.Length);
        var beatmap = Beatmap.FromBytes(bytesPointer);

        beatmap.Convert((Mode)score.GameMode.ToVanillaGameMode());

        var result = GetUserPerformance(score).Calculate(beatmap.Context);

        return result.mode switch
        {
            Mode.Osu => result.osu.ToNullable()!.Value.pp,
            Mode.Taiko => result.taiko.ToNullable()!.Value.pp,
            Mode.Catch => result.fruit.ToNullable()!.Value.pp,
            Mode.Mania => result.mania.ToNullable()!.Value.pp,
            _ => 0
        };
    }

    public static async Task<double> RecalcuteBeatmapDifficulty(BaseSession session, int beatmapId, int mode,
        Mods mods = Mods.None)
    {
        var beatmapBytes = await BeatmapManager.GetBeatmapFile(session, beatmapId);

        if (beatmapBytes == null) return -1;

        var bytesPointer = new Sliceu8(GCHandle.Alloc(beatmapBytes, GCHandleType.Pinned), (uint)beatmapBytes.Length);
        var beatmap = Beatmap.FromBytes(bytesPointer);

        var scoreVanillaGameMode = mode % 4;
        beatmap.Convert((Mode)scoreVanillaGameMode);

        var difficulty = Difficulty.New();
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
    }

    public static async Task<(double, double, double, double)> CalculatePerformancePoints(Session session,
        int beatmapId, int mode, Mods mods = Mods.None)
    {
        var beatmapBytes = await BeatmapManager.GetBeatmapFile(session, beatmapId);

        if (beatmapBytes == null) return (0, 0, 0, 0);

        var bytesPointer = new Sliceu8(GCHandle.Alloc(beatmapBytes, GCHandleType.Pinned), (uint)beatmapBytes.Length);
        var beatmap = Beatmap.FromBytes(bytesPointer);

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
            var performance = Performance.New();
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
    }


    public static async Task<double> CalculateUserWeightedAccuracy(int userId, GameMode mode, Score? score = null)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(userId);
        if (user == null || !user.IsActive(false)) return 0;

        // Get users top scores sorted by pp in descending order
        var userBestScores = await database.ScoreService.GetUserScores(userId, mode, ScoreTableType.Best);

        if (userBestScores.Count == 0 && score == null) return 0;

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

    public static async Task<double> CalculateUserWeightedPerformance(int userId, GameMode mode, Score? score = null)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var user = await database.UserService.GetUser(userId);
        if (user == null || !user.IsActive(false)) return 0;

        // Get users top scores sorted by pp in descending order
        var userBestScores = await database.ScoreService.GetUserScores(userId, mode, ScoreTableType.Best);

        if (userBestScores.Count == 0 && score == null) return 0;

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
        // Ignore Relax mod for more enhanced calculation
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
}