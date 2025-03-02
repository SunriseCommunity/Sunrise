using CSharpFunctionalExtensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using RedisKey = Sunrise.Shared.Objects.Keys.RedisKey;

namespace Sunrise.Shared.Database.Services.Users;

public class UserStatsRanksService
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;

    public UserStatsRanksService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
    }

    public async Task<(long globalRank, long countryRank)> GetUserRanks(User user, GameMode mode, bool addRanksIfNotFound = true)
    {
        var getUserRanksResult = await ResultUtil.TryExecuteAsync(async () =>
        {
            var globalRank = await _databaseService.Redis.SortedSetRank(RedisKey.LeaderboardGlobal(mode), user.Id);
            var countryRank = await _databaseService.Redis.SortedSetRank(RedisKey.LeaderboardCountry(mode, (CountryCode)user.Country), user.Id);

            if (!globalRank.HasValue || !countryRank.HasValue)
            {
                if (!addRanksIfNotFound)
                    throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

                var userStats = await _databaseService.Users.Stats.GetUserStats(user.Id, mode);
                if (userStats == null)
                    throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

                var addOrUpdateUserRanksResult = await AddOrUpdateUserRanks(userStats, user);
                if (addOrUpdateUserRanksResult.IsFailure)
                    throw new ApplicationException(addOrUpdateUserRanksResult.Error);

                var getUserRanksResult = await GetUserRanks(user, mode, false);
                (globalRank, countryRank) = getUserRanksResult;
            }

            return (globalRank.Value + 1, countryRank.Value + 1);
        });

        return getUserRanksResult.IsSuccess ? getUserRanksResult.Value : (long.MaxValue, long.MaxValue);

    }

    public async Task<Result> AddOrUpdateUserRanks(UserStats stats, User user)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var (prevGlobalRank, prevCountryRank) = await GetUserRanks(user, stats.GameMode, false);

            await SortedSetAddOrUpdateUserStats(stats, user);

            var updateUserBestRanksResult = await UpdateUserBestRanks(stats, user, prevGlobalRank, prevCountryRank);
            if (updateUserBestRanksResult.IsFailure)
                throw new ApplicationException(updateUserBestRanksResult.Error);
        });
    }

    public async Task<Result> SetAllUsersRanks(GameMode mode, int branchSize = 20)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            for (var i = 1;; i++)
            {
                var usersStats = await _databaseService.Users.Stats.GetUsersStats(mode,
                    LeaderboardSortType.Pp,
                    options: new QueryOptions(new Pagination(i, branchSize)));

                foreach (var stats in usersStats)
                {
                    await _dbContext.Entry(stats).Reference(s => s.User).LoadAsync();

                    await SortedSetAddOrUpdateUserStats(stats, stats.User);
                    await UpdateUserStatsBestRanks(stats, stats.User);
                }

                await _dbContext.SaveChangesAsync();

                if (usersStats.Count < branchSize)
                    break;
            }
        });
    }

    private async Task<Result> UpdateUserBestRanks(UserStats stats, User user, long prevGlobalRank, long prevCountryRank)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var (globalRank, countryRank) = await GetUserRanks(user, stats.GameMode, false);

            await _databaseService.CommitAsTransactionAsync(async () =>
            {
                var isUserGlobalRankGotLower = globalRank > prevGlobalRank;
                var isUserCountryRankGotLower = countryRank > prevCountryRank;

                if (isUserGlobalRankGotLower)
                {
                    var updateAffectedGlobalUsersStatsResult = await UpdateAffectedUsersStats(stats, user, prevGlobalRank, globalRank, UserStatsRankType.Global);
                    if (updateAffectedGlobalUsersStatsResult.IsFailure)
                        throw new ApplicationException(updateAffectedGlobalUsersStatsResult.Error);
                }

                if (isUserCountryRankGotLower)
                {
                    var updateAffectedCountryUsersStatsResult = await UpdateAffectedUsersStats(stats, user, prevCountryRank, countryRank, UserStatsRankType.Country);
                    if (updateAffectedCountryUsersStatsResult.IsFailure)
                        throw new ApplicationException(updateAffectedCountryUsersStatsResult.Error);
                }

                await UpdateUserStatsBestRanks(stats, user);
            });
        });
    }

    private async Task<Result> UpdateAffectedUsersStats(UserStats stats, User user, long prevRank, long rank, UserStatsRankType type)
    {
        var key = string.Empty;

        switch (type)
        {
            case UserStatsRankType.Global:
                key = RedisKey.LeaderboardGlobal(stats.GameMode);
                break;
            case UserStatsRankType.Country:
                key = RedisKey.LeaderboardCountry(stats.GameMode, (CountryCode)user.Country);
                break;
        }

        var getPromotedUserIdsResult = await _databaseService.Redis
            .SortedSetRangeByRankAsync(key, prevRank - 1, rank - 2);
        if (getPromotedUserIdsResult.IsFailure)
            return Result.Failure(getPromotedUserIdsResult.Error);

        var promotedUserIds = getPromotedUserIdsResult.Value;

        return await _databaseService.CommitAsTransactionAsync(async () =>
        {
            var affectedUsersStats = await _databaseService.Users.Stats.GetUsersStats(
                stats.GameMode,
                LeaderboardSortType.Pp,
                promotedUserIds.Select(id => (int)id).ToList());

            foreach (var userStats in affectedUsersStats)
            {
                await UpdateUserStatsBestRanks(userStats);
            }
        });
    }

    private async Task UpdateUserStatsBestRanks(UserStats stats, User? user = null)
    {
        if (user == null)
        {
            await _dbContext.Entry(stats).Reference(s => s.User).LoadAsync();
            user = stats.User;
        }

        var (globalRank, countryRank) = await GetUserRanks(user, stats.GameMode, false);

        if (stats.BestGlobalRank == null || globalRank < stats.BestGlobalRank)
        {
            stats.BestGlobalRankDate = DateTime.UtcNow;
            stats.BestGlobalRank = globalRank;
        }

        if (stats.BestCountryRank == null || countryRank < stats.BestCountryRank)
        {
            stats.BestCountryRankDate = DateTime.UtcNow;
            stats.BestCountryRank = countryRank;
        }
    }

    private async Task SortedSetAddOrUpdateUserStats(UserStats stats, User user)
    {
        var isUserRestricted = user.IsRestricted();
        var newSortingValue = isUserRestricted ? -1 : stats.PerformancePoints;

        await _databaseService.Redis
            .SortedSetAdd(RedisKey.LeaderboardGlobal(stats.GameMode), stats.UserId, newSortingValue);

        await _databaseService.Redis
            .SortedSetAdd(RedisKey.LeaderboardCountry(stats.GameMode, (CountryCode)user.Country), stats.UserId, newSortingValue);
    }
}