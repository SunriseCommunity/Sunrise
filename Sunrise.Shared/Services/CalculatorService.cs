using CSharpFunctionalExtensions;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions.Performances;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Utils.Calculators;
using Mods = osu.Shared.Mods;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Services;

public class CalculatorService(Lazy<DatabaseService> database, HttpClientService client)
{
    public async Task<Result<PerformanceAttributes, ErrorMessage>> CalculateScorePerformance(BaseSession session, Score score)
    {
        var serializedScore = new CalculateScoreRequest(score)
        {
            Mods = score.Mods.IgnoreNotStandardModsForRecalculation()
        };

        var performanceResult = await client.PostRequestWithBody<PerformanceAttributes>(session, ApiType.CalculateScorePerformance, serializedScore);

        if (performanceResult.IsFailure) return performanceResult;

        var performance = performanceResult.Value;
        performance = performance.ApplyNotStandardModRecalculationsIfNeeded(score);

        return performance;
    }

    public async Task<Result<PerformanceAttributes, ErrorMessage>> CalculateBeatmapPerformance(BaseSession session, int beatmapId, GameMode mode,
        Mods mods = Mods.None, int? combo = null, int? misses = null, float? accuracy = null)
    {
        var requestMods = mods.IgnoreNotStandardModsForRecalculation();

        var performancesResult = await client.SendRequest<List<PerformanceAttributes>>(session,
            ApiType.CalculateBeatmapPerformance,
            [beatmapId, accuracy ?? 100, (int)mode, (int)requestMods, combo, misses]);

        if (performancesResult.IsFailure) return performancesResult.ConvertFailure<PerformanceAttributes>();

        var performances = performancesResult.Value;
        performances = performances.Select(p => p.ApplyNotStandardModRecalculationsIfNeeded(accuracy ?? 100, mods)).ToList();

        return performances.First();
    }

    public async Task<Result<(PerformanceAttributes, PerformanceAttributes, PerformanceAttributes, PerformanceAttributes), ErrorMessage>> CalculatePerformancePoints(BaseSession session,
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

        var requestMods = mods.IgnoreNotStandardModsForRecalculation();

        var performancesResult = await client.SendRequest<List<PerformanceAttributes>>(session,
            ApiType.CalculateBeatmapPerformance,
            [beatmapId, accuraciesString, mode, (int)requestMods, null, null]);

        if (performancesResult.IsFailure) return performancesResult.ConvertFailure<(PerformanceAttributes, PerformanceAttributes, PerformanceAttributes, PerformanceAttributes)>();

        var performances = performancesResult.Value;

        performances = performances
            .Select((p, index) => new
            {
                Performance = p,
                Index = index
            })
            .Select(x => x.Performance.ApplyNotStandardModRecalculationsIfNeeded(accuracies[x.Index], mods))
            .ToList();

        return (performances[0], performances[1], performances[2], performances[3]);
    }

    public async Task<double> CalculateUserWeightedAccuracy(int userId, GameMode mode, Score? score = null)
    {
        var user = await database.Value.Users.GetUser(userId);
        if (user == null) return 0;

        var (userBestScores, _) = await database.Value.Scores.GetUserScores(userId,
            mode,
            ScoreTableType.Best,
            new QueryOptions(true, new Pagination(1, 100))
            {
                IgnoreCountQueryIfExists = true
            });

        return PerformanceCalculator.CalculateUserWeightedAccuracy(userBestScores, score);
    }

    public async Task<double> CalculateUserWeightedPerformance(int userId, GameMode mode, Score? score = null)
    {
        var user = await database.Value.Users.GetUser(userId);
        if (user == null) return 0;

        var (userBestScores, _) = await database.Value.Scores.GetUserScores(userId,
            mode,
            ScoreTableType.Best,
            new QueryOptions(true, new Pagination(1, 100)));

        return PerformanceCalculator.CalculateUserWeightedPerformance(userBestScores, score);
    }
}