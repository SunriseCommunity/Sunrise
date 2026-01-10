using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using RedisKey = Sunrise.Shared.Objects.Keys.RedisKey;

namespace Sunrise.Shared.Tests.Services;

[Collection("Integration tests collection")]
public class UserStatsRanksServiceTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues<GameMode>()
            .Where(mode => mode is GameMode.Standard or GameMode.ScoreV2Mania)
            .Select(mode => new object[]
            {
                mode
            });
    }


    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task DeleteUserCountryRanks_RemovesUserFromLeaderboards(GameMode mode)
    {
        // Arrange
        var user = await CreateTestUserWithStats(mode, 1000);
        var stats = user.UserStats.First(s => s.GameMode == mode);

        // Verify user is on leaderboard
        var (initialRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(user, mode, false);
        initialRank.Should().Be(1);

        // Act
        var result = await Database.Users.Stats.Ranks.DeleteUserCountryRanks(stats, user);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var (globalRank, countryRank) = await Database.Users.Stats.Ranks.GetUserRanks(user, mode, false);
        globalRank.Should().Be(long.MaxValue);
        countryRank.Should().Be(long.MaxValue);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_ReturnsCorrectCountryRank_ForUsersInSameCountry(GameMode mode)
    {
        // Arrange
        var country = _mocker.User.GetRandomCountryCode();
        var user1 = await CreateTestUserWithStats(mode, 3000, country);
        var user2 = await CreateTestUserWithStats(mode, 2000, country);
        var user3 = await CreateTestUserWithStats(mode, 1000, country);

        // Act
        var (_, countryRank1) = await Database.Users.Stats.Ranks.GetUserRanks(user1, mode);
        var (_, countryRank2) = await Database.Users.Stats.Ranks.GetUserRanks(user2, mode);
        var (_, countryRank3) = await Database.Users.Stats.Ranks.GetUserRanks(user3, mode);

        // Assert
        countryRank1.Should().Be(1);
        countryRank2.Should().Be(2);
        countryRank3.Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_CountryRanksAreIndependent_ForDifferentCountries(GameMode mode)
    {
        // Arrange
        var country1 = CountryCode.US;
        var country2 = CountryCode.GB;

        var user1 = await CreateTestUserWithStats(mode, 3000, country1);
        var user2 = await CreateTestUserWithStats(mode, 2000, country2);

        // Act
        var (_, countryRank1) = await Database.Users.Stats.Ranks.GetUserRanks(user1, mode);
        var (_, countryRank2) = await Database.Users.Stats.Ranks.GetUserRanks(user2, mode);

        // Assert 
        countryRank1.Should().Be(1);
        countryRank2.Should().Be(1);
    }

    private async Task<User> CreateTestUserWithStats(GameMode mode, double performancePoints, CountryCode? country = null, bool addToLeaderboard = true)
    {
        var user = _mocker.User.GetRandomUser();

        if (country.HasValue)
        {
            user.Country = country.Value;
        }

        await Database.Users.AddUser(user);

        user.LastOnlineTime = user.LastOnlineTime.ToDatabasePrecision();
        user.RegisterDate = user.RegisterDate.ToDatabasePrecision();

        var stats = new UserStats
        {
            UserId = user.Id,
            GameMode = mode,
            PerformancePoints = performancePoints
        };

        await Database.DbContext.UserStats.AddAsync(stats);
        await Database.DbContext.SaveChangesAsync();

        user.UserStats = [stats];
        stats.User = user;

        if (addToLeaderboard)
        {
            await Database.Users.Stats.Ranks.AddOrUpdateUserRanks(stats, user);
        }

        return user;
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task SetAllUsersRanks_SetsRanksForAllUsers(GameMode mode)
    {
        // Arrange
        var users = await CreateTestUsers(3);

        foreach (var user in users)
        {
            var stats = user.UserStats.FirstOrDefault(s => s.GameMode == mode);

            if (stats != null)
            {
                stats.PerformancePoints = _mocker.GetRandomInteger(length: 3);
                Database.DbContext.UserStats.Update(stats);
            }
        }

        await Database.DbContext.SaveChangesAsync();

        // Act
        var result = await Database.Users.Stats.Ranks.SetAllUsersRanks(mode, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();

        foreach (var user in users)
        {
            await Database.DbContext.Entry(user).ReloadAsync();
            var userStats = await Database.Users.Stats.GetUserStats(user.Id, mode);

            if (userStats != null)
            {
                user.UserStats = [userStats];
            }

            var (globalRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(user, mode, false);
            globalRank.Should().BeLessThan(long.MaxValue);
        }
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task AddOrUpdateUserRanks_SetsNegativeScore_ForRestrictedUser(GameMode mode)
    {
        // Arrange
        var user = await CreateTestUserWithStats(mode, 1000);
        user.AccountStatus = UserAccountStatus.Restricted;
        Database.DbContext.Users.Update(user);
        await Database.DbContext.SaveChangesAsync();

        var stats = user.UserStats.First(s => s.GameMode == mode);

        // Act
        var result = await Database.Users.Stats.Ranks.AddOrUpdateUserRanks(stats, user);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var (globalRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(user, mode, false);
        globalRank.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_ReturnsRanks_WhenUserHasRanks(GameMode mode)
    {
        // Arrange
        var user = await CreateTestUserWithStats(mode, 1000);

        // Act
        var (globalRank, countryRank) = await Database.Users.Stats.Ranks.GetUserRanks(user, mode);

        // Assert
        globalRank.Should().Be(1);
        countryRank.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_ReturnsRanks_ShouldRemovePreviousRanksWhichAreNotInHashTable(GameMode mode)
    {
        // Arrange
        var userId = 1002;

        using var scope = App.Server.Services.CreateScope();
        var redisConnection = scope.ServiceProvider.GetRequiredService<ConnectionMultiplexer>();

        var redisSortedSetDb = redisConnection.GetDatabase(1);
        var redisValue = $"{long.MaxValue - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{userId}";
        await redisSortedSetDb.SortedSetAddAsync(RedisKey.LeaderboardGlobal(mode), redisValue, 10000);

        var user = await CreateTestUserWithStats(mode, 1000);

        // Act
        var (globalRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(user, mode);

        // Assert
        globalRank.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_AddsRanksIfNotFound_WhenAddRanksIfNotFoundIsTrue(GameMode mode)
    {
        // Arrange
        var user = await CreateTestUserWithStats(mode, 1000, addToLeaderboard: false);

        // Act
        var (globalRank, countryRank) = await Database.Users.Stats.Ranks.GetUserRanks(user, mode);

        // Assert
        globalRank.Should().Be(1);
        countryRank.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_ReturnsMaxValue_WhenUserHasNoRanksAndAddRanksIfNotFoundIsFalse(GameMode mode)
    {
        // Arrange
        var user = await CreateTestUserWithStats(mode, 1000, addToLeaderboard: false);

        // Act
        var (globalRank, countryRank) = await Database.Users.Stats.Ranks.GetUserRanks(user, mode, false);

        // Assert
        globalRank.Should().Be(1);
        countryRank.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_ReturnsCorrectRankOrder_ForMultipleUsers(GameMode mode)
    {
        // Arrange
        var user1 = await CreateTestUserWithStats(mode, 3000);
        var user2 = await CreateTestUserWithStats(mode, 2000);
        var user3 = await CreateTestUserWithStats(mode, 1000);

        // Act
        var (globalRank1, _) = await Database.Users.Stats.Ranks.GetUserRanks(user1, mode);
        var (globalRank2, _) = await Database.Users.Stats.Ranks.GetUserRanks(user2, mode);
        var (globalRank3, _) = await Database.Users.Stats.Ranks.GetUserRanks(user3, mode);

        // Assert 
        globalRank1.Should().Be(1);
        globalRank2.Should().Be(2);
        globalRank3.Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task AddOrUpdateUserRanks_AddsUserToLeaderboard(GameMode mode)
    {
        // Arrange
        var user = await CreateTestUserWithStats(mode, 1000, addToLeaderboard: false);
        var stats = user.UserStats.First(s => s.GameMode == mode);

        // Act
        var result = await Database.Users.Stats.Ranks.AddOrUpdateUserRanks(stats, user);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var (globalRank, countryRank) = await Database.Users.Stats.Ranks.GetUserRanks(user, mode, false);
        globalRank.Should().Be(1);
        countryRank.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task AddOrUpdateUserRanks_UpdatesUserRank_WhenPpChanges(GameMode mode)
    {
        // Arrange
        var user1 = await CreateTestUserWithStats(mode, 2000);
        var user2 = await CreateTestUserWithStats(mode, 1000);

        var (initialRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(user2, mode);
        initialRank.Should().Be(2);

        var stats2 = user2.UserStats.First(s => s.GameMode == mode);
        stats2.PerformancePoints = 3000;

        // Act
        var result = await Database.Users.Stats.Ranks.AddOrUpdateUserRanks(stats2, user2);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var (newGlobalRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(user2, mode, false);
        newGlobalRank.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task AddOrUpdateUserRanks_UpdatesBestRank_WhenRankImproves(GameMode mode)
    {
        // Arrange
        var user1 = await CreateTestUserWithStats(mode, 2000);
        var user2 = await CreateTestUserWithStats(mode, 1000);

        var stats2 = user2.UserStats.First(s => s.GameMode == mode);
        stats2.BestGlobalRank = null;
        stats2.BestCountryRank = null;

        stats2.PerformancePoints = 3000;

        // Act
        var result = await Database.Users.Stats.Ranks.AddOrUpdateUserRanks(stats2, user2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        stats2.BestGlobalRank.Should().Be(1);
        stats2.BestCountryRank.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetCountryRanksCount_ReturnsCorrectCount(GameMode mode)
    {
        // Arrange
        var country = _mocker.User.GetRandomCountryCode();
        await CreateTestUserWithStats(mode, 1000, country);
        await CreateTestUserWithStats(mode, 2000, country);
        await CreateTestUserWithStats(mode, 3000, country);

        // Act
        var count = await Database.Users.Stats.Ranks.GetCountryRanksCount(mode, country);

        // Assert
        count.Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetCountryRanksCount_ReturnsZero_WhenNoUsersFromCountry(GameMode mode)
    {
        // Arrange 

        // Act
        var count = await Database.Users.Stats.Ranks.GetCountryRanksCount(mode, CountryCode.AQ);

        // Assert
        count.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_FirstAddedUserHasRank1_WhenBothUsersHaveSameScore(GameMode mode)
    {
        // Arrange
        var samePerformancePoints = 1000.0;
        var firstAddedUser = await CreateTestUserWithStats(mode, samePerformancePoints);
        var secondAddedUser = await CreateTestUserWithStats(mode, samePerformancePoints);

        // Act
        var (firstAddedUserRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(firstAddedUser, mode);
        var (secondAddedUserRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(secondAddedUser, mode);

        // Assert
        firstAddedUserRank.Should().Be(1);
        secondAddedUserRank.Should().Be(2);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_SecondUserBecomesRank1_WhenScoreIsSlightlyHigher(GameMode mode)
    {
        // Arrange
        var samePerformancePoints = 1000.0;
        var slightlyHigherPerformancePoints = 1000.001;

        var firstAddedUser = await CreateTestUserWithStats(mode, samePerformancePoints);
        var secondAddedUser = await CreateTestUserWithStats(mode, samePerformancePoints);

        var (firstAddedUserRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(firstAddedUser, mode);
        var (secondAddedUserRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(secondAddedUser, mode);

        firstAddedUserRank.Should().Be(1);
        secondAddedUserRank.Should().Be(2);

        var secondUserStats = secondAddedUser.UserStats.First(s => s.GameMode == mode);
        secondUserStats.PerformancePoints = slightlyHigherPerformancePoints;

        // Act
        await Database.Users.Stats.Ranks.AddOrUpdateUserRanks(secondUserStats, firstAddedUser);

        // Assert
        var (firstUserRankAfterUpdate, _) = await Database.Users.Stats.Ranks.GetUserRanks(firstAddedUser, mode);
        var (secondUserRankAfterUpdate, _) = await Database.Users.Stats.Ranks.GetUserRanks(secondAddedUser, mode);

        firstUserRankAfterUpdate.Should().Be(2);
        secondUserRankAfterUpdate.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task GetUserRanks_FirstUserBecomesRank2_WhenScoreUpdatedToSameValue(GameMode mode)
    {
        // Arrange
        var samePerformancePoints = 1000.0;
        var firstAddedUser = await CreateTestUserWithStats(mode, samePerformancePoints);
        var secondAddedUser = await CreateTestUserWithStats(mode, samePerformancePoints);

        var (initialFirstUserRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(firstAddedUser, mode);
        initialFirstUserRank.Should().Be(1);

        var firstUserStats = firstAddedUser.UserStats.First(s => s.GameMode == mode);
        firstUserStats.PerformancePoints = samePerformancePoints;

        // Act
        await Database.Users.Stats.Ranks.AddOrUpdateUserRanks(firstUserStats, firstAddedUser);

        // Assert
        var (firstUserRankAfterUpdate, _) = await Database.Users.Stats.Ranks.GetUserRanks(firstAddedUser, mode);
        var (secondUserRankAfterUpdate, _) = await Database.Users.Stats.Ranks.GetUserRanks(secondAddedUser, mode);

        firstUserRankAfterUpdate.Should().Be(2);
        secondUserRankAfterUpdate.Should().Be(1);
    }
}