using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Utils.Calculators;
using Mods = osu.Shared.Mods;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Services;

public class CalculatorService(Lazy<BeatmapService> beatmapService, Lazy<DatabaseService> database)
{
    public async Task<double> CalculatePerformancePoints(BaseSession session, Score score)
    {
        var beatmapBytes = await beatmapService.Value.GetBeatmapFile(session, score.BeatmapId);
        if (beatmapBytes == null) return 0;

        return PerformanceCalculator.CalculatePerformancePoints(beatmapBytes, score);
    }

    public async Task<double> RecalculateBeatmapDifficulty(BaseSession session, int beatmapId, int mode,
        Mods mods = Mods.None)
    {
        var beatmapBytes = await beatmapService.Value.GetBeatmapFile(session, beatmapId);
        if (beatmapBytes == null) return 0;

        return PerformanceCalculator.RecalculateBeatmapDifficulty(beatmapBytes, mode, mods);
    }

    public async Task<(double, double, double, double)> CalculatePerformancePoints(BaseSession session,
        int beatmapId, int mode, Mods mods = Mods.None)
    {
        var beatmapBytes = await beatmapService.Value.GetBeatmapFile(session, beatmapId);
        if (beatmapBytes == null) return (0, 0, 0, 0);

        return PerformanceCalculator.CalculatePerformancePoints(beatmapBytes, mode, mods);
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