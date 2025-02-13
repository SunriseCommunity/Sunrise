using System.Net;
using System.Net.Http.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Types.Enums;

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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/leaderboard?type={leaderboardType}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestLeaderboardInvalidLimit(string limit)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/leaderboard?limit={limit}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestLeaderboardUserInvalidPage(string page)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/leaderboard?page={page}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestLeaderboard(GameMode gamemode)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var usersNumber = _mocker.GetRandomInteger(minInt: 2, maxInt: 5);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var userIdsSortedByPp = new List<int>();

        for (var i = 0; i < usersNumber; i++)
        {
            var user = _mocker.User.GetRandomUser();
            user = await CreateTestUser(user);

            userIdsSortedByPp.Add(user.Id);

            var stats = await database.UserService.Stats.GetUserStats(user.Id, gamemode);
            if (stats == null)
                throw new Exception("User stats not found");

            stats.PerformancePoints = i * 100;

            await database.UserService.Stats.UpdateUserStats(stats);
        }

        // Act
        var response = await client.GetAsync($"user/leaderboard?mode={(int)gamemode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(responseData);

        Assert.Equivalent(userIdsSortedByPp.LastOrDefault(), responseData.Users.FirstOrDefault()?.User.Id);
    }

    [Fact]
    public async Task TestLeaderboardWithoutRestrictedUsers()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var usersNumber = _mocker.GetRandomInteger(minInt: 2, maxInt: 5);

        for (var i = 0; i < usersNumber; i++)
        {
            await CreateTestUser();
        }

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var restrictedUser = await CreateTestUser();

        await database.UserService.Moderation.RestrictPlayer(restrictedUser.Id, 0, "Test");

        // Act
        var response = await client.GetAsync("user/leaderboard?mode=0&limit=10");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(responseData);

        Assert.DoesNotContain(responseData.Users, x => x.User.Id == restrictedUser.Id);
    }

    [Fact]
    public async Task TestLeaderboardLimitAndPage()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var usersNumber = _mocker.GetRandomInteger(minInt: 3, maxInt: 5);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var gamemode = _mocker.Score.GetRandomGameMode();

        var userIdsSortedByPp = new List<int>();

        for (var i = 0; i < usersNumber; i++)
        {
            var user = _mocker.User.GetRandomUser();
            user = await CreateTestUser(user);

            userIdsSortedByPp.Add(user.Id);

            var stats = await database.UserService.Stats.GetUserStats(user.Id, gamemode);
            if (stats == null)
                throw new Exception("User stats not found");

            stats.PerformancePoints = i * 100;

            await database.UserService.Stats.UpdateUserStats(stats);
        }

        // Act
        var response = await client.GetAsync($"user/leaderboard?mode={(int)gamemode}&limit=1&page=1");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(responseData);

        Assert.Single(responseData.Users);
        Assert.Equivalent(userIdsSortedByPp.SkipLast(1).LastOrDefault(), responseData.Users.FirstOrDefault()?.User.Id);
    }
}