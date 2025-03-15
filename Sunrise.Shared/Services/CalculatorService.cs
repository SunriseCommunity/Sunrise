using CSharpFunctionalExtensions;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Utils.Calculators;
using Mods = osu.Shared.Mods;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Services;

public class CalculatorService(Lazy<BeatmapService> beatmapService, Lazy<DatabaseService> database, HttpClientService client)
{
    public async Task<double> CalculatePerformancePoints(BaseSession session, Score score)
    {

        var serializedScore = new CalculateScoreRequest(score);

        // TODO: Replace with proper calculation
        // Ignore Relax mod for more enhanced calculation for Relax

        serializedScore.Mods &= ~Mods.Relax;

        var performance = await client.SendRequestWithBody<PerformanceAttributes>(session, ApiType.CalculateScorePerformance, serializedScore);

        if (performance == null) return 0;

        return performance.PerformancePoints;
    }

    public async Task<Result<PerformanceAttributes>> RecalculateBeatmapPerformance(BaseSession session, int beatmapId, int mode,
        Mods mods = Mods.None)
    {
        // TODO: Replace with proper calculation
        // Ignore Relax mod for more enhanced calculation for Relax

        mods &= ~Mods.Relax;
        
        var performance = await client.SendRequest<List<PerformanceAttributes>?>(session,
            ApiType.CalculateBeatmapPerformance,
            [beatmapId, null, mode, (int)mods, null, null]);

        if (performance == null) return Result.Failure<PerformanceAttributes>("Failed while recalculating beatmap performance");

        return performance.First();
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
        
        // TODO: Replace with proper calculation
        // Ignore Relax mod for more enhanced calculation for Relax

        mods &= ~Mods.Relax;

        var performances = await client.SendRequest<List<PerformanceAttributes>?>(session,
            ApiType.CalculateBeatmapPerformance,
            [beatmapId, accuraciesString, mode, (int)mods, null, null]);

        if (performances == null || performances.Count == 0) return Result.Failure<(PerformanceAttributes, PerformanceAttributes, PerformanceAttributes, PerformanceAttributes)>("Error while calculating performance points");

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