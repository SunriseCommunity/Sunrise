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

public class UserStatsService(
    ILogger<UserStatsService> logger,
    Lazy<DatabaseService> databaseService,
    SunriseDbContext dbContext,
    UserStatsSnapshotService userStatsSnapshotService,
    UserStatsRanksService userStatsRanksService)
{
    private readonly ILogger _logger = logger;

    public UserStatsSnapshotService Snapshots { get; } = userStatsSnapshotService;
    public UserStatsRanksService Ranks { get; } = userStatsRanksService;

    public async Task<Result> AddUserStats(UserStats stats, User user)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UserStats.Add(stats);
            await dbContext.SaveChangesAsync();
            
            var addOrUpdateUserRanksResult = await Ranks.AddOrUpdateUserRanks(stats, user);
            if (addOrUpdateUserRanksResult.IsFailure)
                throw new ApplicationException(addOrUpdateUserRanksResult.Error);
        });
    }

    public async Task<Result> UpdateUserStats(UserStats stats, User user)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var addOrUpdateUserRanksResult = await Ranks.AddOrUpdateUserRanks(stats, user);
            if (addOrUpdateUserRanksResult.IsFailure)
                throw new ApplicationException(addOrUpdateUserRanksResult.Error);

            dbContext.UpdateEntity(stats);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<UserStats?> GetUserStats(int userId, GameMode mode, CancellationToken ct = default)
    {
        var stats = await dbContext.UserStats.Where(e => e.UserId == userId && e.GameMode == mode).FirstOrDefaultAsync(cancellationToken: ct);

        if (stats == null)
        {
            var user = await databaseService.Value.Users.GetUser(userId, ct: ct);
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

    public async Task<List<UserStats>> GetUsersStats(GameMode mode, LeaderboardSortType leaderboardSortType, List<int>? userIds = null, QueryOptions? options = null, bool addMissingUserStats = true, CancellationToken ct = default)
    {
        var statsQuery = dbContext.UserStats.Where(e => e.GameMode == mode);

        statsQuery = leaderboardSortType switch
        {
            LeaderboardSortType.Pp => statsQuery.OrderByDescending(e => e.PerformancePoints)
                .ThenByDescending(e => dbContext.Scores.Where(s => s.UserId == e.UserId && s.GameMode == mode)
                    .OrderBy(s => s.WhenPlayed).FirstOrDefault())
                .ThenBy(e => e.Id),
            LeaderboardSortType.Score => statsQuery.OrderByDescending(e => e.RankedScore)
                .ThenByDescending(e => dbContext.Scores.Where(s => s.UserId == e.UserId && s.GameMode == mode)
                    .OrderBy(s => s.WhenPlayed).FirstOrDefault())
                .ThenBy(e => e.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(leaderboardSortType), leaderboardSortType, null)
        };

        if (userIds != null) statsQuery = statsQuery.Where(us => userIds.Contains(us.UserId));

        var stats = await statsQuery
            .FilterValidUserStats()
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        var isSomeStatsMissing = userIds != null && stats.Count != userIds.Count;

        if (isSomeStatsMissing && addMissingUserStats)
        {
            var users = await databaseService.Value.Users.GetValidUsers(userIds, ct: ct);
            if (users.Count == stats.Count)
                return stats; // We return only valid users stats, so if we can't find user by user id in valid users, user stats can't exist

            var usersWithoutStats = users.Where(u => !stats.Select(us => us.UserId).Contains(u.Id));

            var transactionResult = await databaseService.Value.CommitAsTransactionAsync(async () =>
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
                },
                ct);

            if (transactionResult.IsFailure)
                throw new Exception(transactionResult.Error);

            return await GetUsersStats(mode, leaderboardSortType, userIds, options, false);
        }

        return stats;
    }
}