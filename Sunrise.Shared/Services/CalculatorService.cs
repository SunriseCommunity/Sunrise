using System.Net;
using CSharpFunctionalExtensions;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
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

[TraceExecution]
public class CalculatorService(Lazy<DatabaseService> database, HttpClientService client)
{
    public async Task<Result<PerformanceAttributes, ErrorMessage>> CalculateScorePerformance(BaseSession session, Score score, int? retryCount = 1, bool shouldSendRateLimitWarning = true, CancellationToken ct = default)
    {
        var serializedScore = new CalculateScoreRequest(score)
        {
            Mods = score.Mods.IgnoreNotStandardModsForRecalculation()
        };

        // TODO: Since this logic is only required to not accidentally lose submitted scores if we cant fetch beatmaps (observatory/mirrors are down, etc.), 
        // I would suggest writing scores as is in the database and have a background task that retries fetching beatmaps for scores that dont have them until they are found. (This would also allow the server to be rebooted without losing scores)
        using var timeoutCts = retryCount == int.MaxValue
            ? new CancellationTokenSource()
            : new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);

        var performanceResult = await client.PostRequestWithBody<PerformanceAttributes>(session, ApiType.CalculateScorePerformance, serializedScore, shouldSendRateLimitWarning: shouldSendRateLimitWarning, ct: linkedCts.Token);

        while (retryCount > 0 && !linkedCts.IsCancellationRequested && !IsValidResult(performanceResult))
        {
            retryCount--;

            performanceResult = await client.PostRequestWithBody<PerformanceAttributes>(session, ApiType.CalculateScorePerformance, serializedScore, shouldSendRateLimitWarning: shouldSendRateLimitWarning, ct: linkedCts.Token);

            if (!IsValidResult(performanceResult) && !linkedCts.IsCancellationRequested)
            {
                if (retryCount > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), linkedCts.Token);
                }
            }
        }

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

    public async Task<double> CalculateUserWeightedAccuracy(User user, GameMode mode, Score? score = null)
    {
        if (!user.IsActive()) return -1;

        var (userBestScores, _) = await database.Value.Scores.GetUserScores(user.Id,
            mode,
            ScoreTableType.Best,
            new QueryOptions(true, new Pagination(1, 100))
            {
                IgnoreCountQueryIfExists = true
            });

        return PerformanceCalculator.CalculateUserWeightedAccuracy(userBestScores, score);
    }

    public async Task<double> CalculateUserWeightedPerformance(User user, GameMode mode, Score? score = null)
    {
        if (!user.IsActive()) return -1;

        var (userBestScores, _) = await database.Value.Scores.GetUserScores(user.Id,
            mode,
            ScoreTableType.Best,
            new QueryOptions(true, new Pagination(1, 100)));

        return PerformanceCalculator.CalculateUserWeightedPerformance(userBestScores, score);
    }

    private bool IsValidResult<T>(Result<T, ErrorMessage> result)
    {
        var isNotFoundResult = result.IsFailure && result.Error.Status == HttpStatusCode.NotFound;
        var isValidResult = result.IsSuccess || isNotFoundResult;

        return isValidResult;
    }
}