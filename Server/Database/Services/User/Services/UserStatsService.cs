using DatabaseWrapper.Core;
using ExpressionTree;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Sqlite;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Database.Services.User.Services;

public class UserStatsService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public UserStatsService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserStatsService>();

        _services = services;

        _database = database;
        _redis = redis;

        Snapshots = new UserStatsSnapshotService(_redis, _database);
    }

    public UserStatsSnapshotService Snapshots { get; }

    public async Task<UserStats?> GetUserStats(int userId, GameMode mode, bool useCache = true)
    {
        var cachedStats = await _redis.Get<UserStats?>(RedisKey.UserStats(userId, mode));

        if (cachedStats != null && useCache) return cachedStats;

        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode",
            OperatorEnum.Equals,
            (int)mode);
        var stats = await _database.SelectFirstAsync<UserStats?>(exp);

        if (stats == null)
        {
            var user = await _services.UserService.GetUser(userId);
            if (user == null) return null;

            _logger.LogCritical($"User stats not found for user {userId} in mode {mode}. Creating new stats.");

            stats = new UserStats
            {
                UserId = userId,
                GameMode = mode
            };

            await InsertUserStats(stats);
            stats = await _database.SelectFirstAsync<UserStats?>(exp);
        }

        await _redis.Set(RedisKey.UserStats(userId, mode), stats);

        return stats;
    }

    public async Task<List<UserStats>> GetAllUserStats(GameMode mode, LeaderboardSortType leaderboardSortType,
        bool useCache = true)
    {
        var cachedStats = await _redis.Get<List<UserStats>>(RedisKey.AllUserStats(mode));

        if (cachedStats != null && useCache) return cachedStats;

        var exp = new Expr("GameMode", OperatorEnum.Equals, (int)mode);

        var stats = await _database.SelectManyAsync<UserStats>(exp,
        [
            leaderboardSortType switch
            {
                LeaderboardSortType.Pp => new ResultOrder("PerformancePoints", OrderDirectionEnum.Descending),
                LeaderboardSortType.Score => new ResultOrder("TotalScore", OrderDirectionEnum.Descending),
                _ => throw new ArgumentOutOfRangeException(nameof(leaderboardSortType), leaderboardSortType, null)
            }
        ]);

        if (stats == null) return [];

        await _redis.Set(RedisKey.AllUserStats(mode), stats);

        return stats;
    }

    public async Task UpdateUserStats(UserStats stats)
    {
        stats = await SetUserRank(stats);
        stats = await _database.UpdateAsync(stats);

        await _redis.Set(RedisKey.UserStats(stats.UserId, stats.GameMode), stats);
    }

    public async Task InsertUserStats(UserStats stats)
    {
        stats = await SetUserRank(stats);
        stats = await _database.InsertAsync(stats);

        await _redis.Set(RedisKey.UserStats(stats.UserId, stats.GameMode), stats);
    }

    public async Task<long> GetUserRank(int userId, GameMode mode)
    {
        var rank = await _redis.SortedSetRank(RedisKey.LeaderboardGlobal(mode), userId);

        if (!rank.HasValue)
        {
            rank = await _redis.SortedSetRank(RedisKey.LeaderboardGlobal(mode), userId);
            await SetUserRank(userId, mode);
        }

        return rank.HasValue ? rank.Value + 1 : -1;
    }

    public async Task<long> GetUserCountryRank(int userId, GameMode mode)
    {
        var user = await _services.UserService.GetUser(userId);
        if (user == null) return -1;

        var rank = await _redis.SortedSetRank(RedisKey.LeaderboardCountry(mode, (CountryCodes)user.Country), userId);

        if (!rank.HasValue)
        {
            rank = await _redis.SortedSetRank(RedisKey.LeaderboardCountry(mode, (CountryCodes)user.Country), userId);
            await SetUserRank(userId, mode);
        }

        return rank.HasValue ? rank.Value + 1 : -1;
    }

    private async Task<UserStats> SetUserRank(int userId, GameMode mode)
    {
        var stats = await GetUserStats(userId, mode);
        if (stats == null) throw new Exception("User stats not found for user " + userId);

        stats = await SetUserRank(stats);
        return stats;
    }

    private async Task<UserStats> SetUserRank(UserStats stats)
    {
        var user = await _services.UserService.GetUser(stats.UserId);

        await _redis.SortedSetAdd(RedisKey.LeaderboardGlobal(stats.GameMode), stats.UserId, stats.PerformancePoints);
        await _redis.SortedSetAdd(RedisKey.LeaderboardCountry(stats.GameMode, (CountryCodes)user.Country),
            stats.UserId,
            stats.PerformancePoints);

        var newRank = await GetUserRank(stats.UserId, stats.GameMode);
        var newCountryRank = await GetUserCountryRank(stats.UserId, stats.GameMode);

        if (newRank < (stats.BestGlobalRank ?? long.MaxValue))
        {
            stats.BestGlobalRankDate = DateTime.UtcNow;
            stats.BestGlobalRank = newRank;
        }

        if (newCountryRank < (stats.BestCountryRank ?? long.MaxValue))
        {
            stats.BestCountryRankDate = DateTime.UtcNow;
            stats.BestCountryRank = newCountryRank;
        }

        return stats;
    }


    private async Task RemoveUserRank(int userId, GameMode mode)
    {
        var user = await _services.UserService.GetUser(userId);
        if (user == null) return;

        await _redis.SortedSetRemove(RedisKey.LeaderboardGlobal(mode), userId);
        await _redis.SortedSetRemove(RedisKey.LeaderboardCountry(mode, (CountryCodes)user.Country), userId);
    }

    public async Task SetAllUserRanks(GameMode mode)
    {
        var usersStats = await GetAllUserStats(mode, LeaderboardSortType.Pp);
        if (usersStats.Count == 0) return;

        usersStats.Sort((x, y) => y.PerformancePoints.CompareTo(x.PerformancePoints));

        foreach (var stats in usersStats)
        {
            await UpdateUserStats(stats);
        }
    }
}