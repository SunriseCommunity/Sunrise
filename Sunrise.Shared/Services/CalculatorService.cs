using CSharpFunctionalExtensions;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions.Performances;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Utils.Calculators;
using Mods = osu.Shared.Mods;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Services;

public class CalculatorService(Lazy<DatabaseService> database, HttpClientService client)
{
    /// <summary>
    ///     Temporary value to disable all custom not standard mods calculations,
    ///     will be removed in the next versions.
    /// </summary>
    private readonly bool IS_USING_CUSTOM_PP_CALCULATION = false;

    public async Task<Result<PerformanceAttributes>> CalculateScorePerformance(BaseSession session, Score score)
    {
        var serializedScore = new CalculateScoreRequest(score);

        score.Mods.IgnoreNotStandardModsForRecalculation();

        var performance = await client.SendRequestWithBody<PerformanceAttributes>(session, ApiType.CalculateScorePerformance, serializedScore);

        if (performance == null) return Result.Failure<PerformanceAttributes>("Failed while calculating score performance");

        if (IS_USING_CUSTOM_PP_CALCULATION)
            performance.ApplyNotStandardModRecalculationsIfNeeded(score);

        return performance;
    }

    public async Task<Result<PerformanceAttributes>> CalculateBeatmapPerformance(BaseSession session, int beatmapId, int mode,
        Mods mods = Mods.None, int? combo = null, int? misses = null)
    {
        mods.IgnoreNotStandardModsForRecalculation();

        var performances = await client.SendRequest<List<PerformanceAttributes>?>(session,
            ApiType.CalculateBeatmapPerformance,
            [beatmapId, 100, mode, (int)mods, combo, misses]);

        if (performances == null) return Result.Failure<PerformanceAttributes>("Failed while Calculating beatmap performance");

        if (IS_USING_CUSTOM_PP_CALCULATION)
            performances.ForEach(p => p.ApplyNotStandardModRecalculationsIfNeeded(100, mods));

        return performances.First();
    }

    public async Task<Result<(PerformanceAttributes, PerformanceAttributes, PerformanceAttributes, PerformanceAttributes)>> CalculatePerformancePoints(BaseSession session,
        int beatmapId, int mode, Mods mods = Mods.None)
    {
        var accuracies = new List<double>
        {
            100,
            99,
            98,
            95
        };

        var accuraciesString = string.Join("&acc=", accuracies);

        mods.IgnoreNotStandardModsForRecalculation();

        var performances = await client.SendRequest<List<PerformanceAttributes>?>(session,
            ApiType.CalculateBeatmapPerformance,
            [beatmapId, accuraciesString, mode, (int)mods, null, null]);

        if (performances == null || performances.Count == 0) return Result.Failure<(PerformanceAttributes, PerformanceAttributes, PerformanceAttributes, PerformanceAttributes)>("Error while calculating performance points");

        if (IS_USING_CUSTOM_PP_CALCULATION)
            performances
                .Select((p, index) => new
                {
                    Performance = p,
                    Index = index
                })
                .ToList()
                .ForEach(x => x.Performance.ApplyNotStandardModRecalculationsIfNeeded(accuracies[x.Index], mods));

        return (performances[0], performances[1], performances[2], performances[3]);
    }

    public async Task<double> CalculateUserWeightedAccuracy(int userId, GameMode mode, Score? score = null)
    {
        var user = await database.Value.Users.GetUser(userId);
        if (user == null || !user.IsActive(false)) return 0;

        var (userBestScores, _) = await database.Value.Scores.GetUserScores(userId,
            mode,
            ScoreTableType.Best,
            new QueryOptions(true, new Pagination(1, 100)));

        return PerformanceCalculator.CalculateUserWeightedAccuracy(userBestScores, score);
    }

    public async Task<double> CalculateUserWeightedPerformance(int userId, GameMode mode, Score? score = null)
    {
        var user = await database.Value.Users.GetUser(userId);
        if (user == null || !user.IsActive(false)) return 0;

        var (userBestScores, _) = await database.Value.Scores.GetUserScores(userId,
            mode,
            ScoreTableType.Best,
            new QueryOptions(true, new Pagination(1, 100)));

        return PerformanceCalculator.CalculateUserWeightedPerformance(userBestScores, score);
    }
}