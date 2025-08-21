using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserLeaderboardTests : ApiTest
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Select(mode => new object[]
        {
            mode
        });
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("100")]
    [InlineData("test")]
    public async Task TestLeaderboardInvalidLeaderboardType(string leaderboardType)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/leaderboard?type={leaderboardType}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestLeaderboardInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/leaderboard?limit={limit}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestLeaderboardUserInvalidPage(string page)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/leaderboard?page={page}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestLeaderboard(GameMode gamemode)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var usersNumber = _mocker.GetRandomInteger(minInt: 2, maxInt: 5);

        var userIdsSortedByPp = new List<int>();

        for (var i = 0; i < usersNumber; i++)
        {
            var user = _mocker.User.GetRandomUser();
            user = await CreateTestUser(user);

            userIdsSortedByPp.Add(user.Id);

            var stats = await Database.Users.Stats.GetUserStats(user.Id, gamemode);
            if (stats == null)
                throw new Exception("User stats not found");

            stats.PerformancePoints = i * 100;

            await Database.Users.Stats.UpdateUserStats(stats, user);
        }

        // Act
        var response = await client.GetAsync($"user/leaderboard?mode={(int)gamemode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<LeaderboardResponse>();
        Assert.NotNull(responseData);

        Assert.Equivalent(userIdsSortedByPp.LastOrDefault(), responseData.Users.FirstOrDefault()?.User.Id);
    }

    [Fact]
    public async Task TestLeaderboardWithoutRestrictedUsers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var usersNumber = _mocker.GetRandomInteger(minInt: 2, maxInt: 5);

        for (var i = 0; i < usersNumber; i++)
        {
            await CreateTestUser();
        }

        var restrictedUser = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(restrictedUser.Id, null, "Test");

        // Act
        var response = await client.GetAsync("user/leaderboard?mode=0&limit=10");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<LeaderboardResponse>();
        Assert.NotNull(responseData);

        Assert.DoesNotContain(responseData.Users, x => x.User.Id == restrictedUser.Id);
    }

    [Fact]
    public async Task TestLeaderboardLimitAndPage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var usersNumber = _mocker.GetRandomInteger(minInt: 3, maxInt: 5);

        var gamemode = _mocker.Score.GetRandomGameMode();

        var userIdsSortedByPp = new List<int>();

        for (var i = 0; i < usersNumber; i++)
        {
            var user = _mocker.User.GetRandomUser();
            user = await CreateTestUser(user);

            userIdsSortedByPp.Add(user.Id);

            var stats = await Database.Users.Stats.GetUserStats(user.Id, gamemode);
            if (stats == null)
                throw new Exception("User stats not found");

            stats.PerformancePoints = i * 100;

            await Database.Users.Stats.UpdateUserStats(stats, user);
        }

        // Act
        var response = await client.GetAsync($"user/leaderboard?mode={(int)gamemode}&limit=1&page=2");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<LeaderboardResponse>();
        Assert.NotNull(responseData);

        Assert.Single(responseData.Users);
        Assert.Equivalent(userIdsSortedByPp.SkipLast(1).LastOrDefault(), responseData.Users.FirstOrDefault()?.User.Id);
    }
}