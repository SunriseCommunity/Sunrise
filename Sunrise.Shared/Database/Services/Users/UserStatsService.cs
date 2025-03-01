using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Services.Users;

public class UserStatsService
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;
    private readonly ILogger _logger;

    public UserStatsService(DatabaseService databaseService)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserStatsService>();

        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;

        Snapshots = new UserStatsSnapshotService(_databaseService);
        Ranks = new UserStatsRanksService(_databaseService);
    }

    public UserStatsSnapshotService Snapshots { get; }
    public UserStatsRanksService Ranks { get; }

    public async Task<Result> AddUserStats(UserStats stats, User user)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var addOrUpdateUserRanksResult = await Ranks.AddOrUpdateUserRanks(stats, user);
            if (addOrUpdateUserRanksResult.IsFailure)
                throw new ApplicationException(addOrUpdateUserRanksResult.Error);

            _dbContext.UserStats.Add(stats);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> UpdateUserStats(UserStats stats, User user)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var addOrUpdateUserRanksResult = await Ranks.AddOrUpdateUserRanks(stats, user);
            if (addOrUpdateUserRanksResult.IsFailure)
                throw new ApplicationException(addOrUpdateUserRanksResult.Error);

            _dbContext.UpdateEntity(stats);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<UserStats?> GetUserStats(int userId, GameMode mode)
    {
        var stats = await _dbContext.UserStats.Where(e => e.UserId == userId && e.GameMode == mode).FirstOrDefaultAsync();

        if (stats == null)
        {
            var user = await _databaseService.Users.GetUser(userId);
            if (user == null) return null;

            _logger.LogCritical($"User stats not found for user (id: {userId}) in mode {mode}. Creating new stats.");

            stats = new UserStats
            {
                UserId = user.Id,
                GameMode = mode
            };

            await AddUserStats(stats, user);
        }

        return stats;
    }

    public async Task<List<UserStats>> GetUsersStats(GameMode mode, LeaderboardSortType leaderboardSortType, List<int>? userIds = null, QueryOptions? options = null, bool addMissingUserStats = true)
    {
        var statsQuery = _dbContext.UserStats.Where(e => e.GameMode == mode);

        statsQuery = leaderboardSortType switch
        {
            LeaderboardSortType.Pp => statsQuery.OrderByDescending(e => e.PerformancePoints),
            LeaderboardSortType.Score => statsQuery.OrderByDescending(e => e.TotalScore),
            _ => throw new ArgumentOutOfRangeException(nameof(leaderboardSortType), leaderboardSortType, null)
        };

        if (userIds != null) statsQuery = statsQuery.Where(us => userIds.Contains(us.UserId));

        var stats = await statsQuery
            .FilterValidUserStats()
            .UseQueryOptions(options)
            .ToListAsync();

        var isSomeStatsMissing = userIds != null && stats.Count != userIds.Count;

        if (isSomeStatsMissing && addMissingUserStats)
        {
            var users = await _databaseService.Users.GetValidUsers(ids: userIds);
            if (users.Count == stats.Count)
                return stats; // We return only valid users stats, so if we can't find user by user id in valid users, user stats can't exist

            var usersWithoutStats = users.Where(u => !stats.Select(us => us.UserId).Contains(u.Id));

            var transactionResult = await _databaseService.CommitAsTransactionAsync(async () =>
            {
                foreach (var user in usersWithoutStats)
                {
                    _logger.LogCritical($"User stats not found for user (id: {user.Id}) in mode {mode}. Creating new stats.");
                    await AddUserStats(new UserStats
                        {
                            UserId = user.Id,
                            GameMode = mode
                        },
                        user);
                }
            });

            if (transactionResult.IsFailure)
                throw new Exception(transactionResult.Error);

            return await GetUsersStats(mode, leaderboardSortType, userIds, options, false);
        }

        return stats;
    }
}