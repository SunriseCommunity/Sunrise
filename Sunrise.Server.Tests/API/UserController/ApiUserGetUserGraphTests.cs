using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserGetUserGraphTests : ApiTest
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Select(mode => new object[]
        {
            mode
        });
    }

    [Fact]
    public async Task TestGetUserGraphUserNotFound()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var userId = _mocker.GetRandomInteger();
        var gamemode = _mocker.Score.GetRandomGameMode();

        // Act
        var response = await client.GetAsync($"user/{userId}/graph?mode={(int)gamemode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("User not found", responseContent?.Error);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserGraphInvalidRoute(string userId)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/graph");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }


    [Fact]
    public async Task TestGetUserGraphUserRestrictedGraphNotFound()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var gamemode = _mocker.Score.GetRandomGameMode();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={(int)gamemode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("User is restricted", responseContent?.Error);
    }

    [Fact]
    public async Task TestGetUserGraph()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var gamemode = _mocker.Score.GetRandomGameMode();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={(int)gamemode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseSnapshots = await response.Content.ReadFromJsonAsync<StatsSnapshotsResponse>();
        Assert.NotNull(responseSnapshots);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var userRank = await database.UserService.Stats.GetUserRank(user.Id, gamemode);

        Assert.Equal(userRank, responseSnapshots.Snapshots.FirstOrDefault()?.Rank);
    }

    [Fact]
    public async Task TestGetUserGraphCheckSorting()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var gamemode = _mocker.Score.GetRandomGameMode();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var snapshots = new List<StatsSnapshot>();

        for (var i = 0; i < 5; i++)
        {
            var snapshot = _mocker.User.GetRandomStatsSnapshot();
            snapshot.SavedAt = _mocker.GetRandomDateTime();
            snapshots.Add(snapshot);
        }

        var userStatsSnapshot = new UserStatsSnapshot
        {
            UserId = user.Id,
            GameMode = gamemode
        };

        userStatsSnapshot.SetSnapshots(snapshots);
        await database.UserService.Stats.Snapshots.InsertUserStatsSnapshot(userStatsSnapshot);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={(int)gamemode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseSnapshots = await response.Content.ReadFromJsonAsync<StatsSnapshotsResponse>();
        Assert.NotNull(responseSnapshots);

        var sortedSnapshots = snapshots.OrderBy(x => x.SavedAt).ToList();

        var firstSnapshot = responseSnapshots.Snapshots.FirstOrDefault();
        Assert.NotNull(firstSnapshot);
        Assert.Equal(sortedSnapshots.First().SavedAt, firstSnapshot.SavedAt);

        var lastSnapshot = responseSnapshots.Snapshots.SkipLast(1).LastOrDefault();
        Assert.NotNull(lastSnapshot);
        Assert.Equal(sortedSnapshots.Last().SavedAt, lastSnapshot.SavedAt);
    }

    [Fact]
    public async Task TestGetUserGraphCheckLimit()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var gamemode = _mocker.Score.GetRandomGameMode();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var snapshots = new List<StatsSnapshot>();

        for (var i = 0; i < 100; i++)
        {
            var snapshot = _mocker.User.GetRandomStatsSnapshot();
            snapshot.SavedAt = _mocker.GetRandomDateTime();
            snapshots.Add(snapshot);
        }

        var userStatsSnapshot = new UserStatsSnapshot
        {
            UserId = user.Id,
            GameMode = gamemode
        };

        userStatsSnapshot.SetSnapshots(snapshots);
        await database.UserService.Stats.Snapshots.InsertUserStatsSnapshot(userStatsSnapshot);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={(int)gamemode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseSnapshots = await response.Content.ReadFromJsonAsync<StatsSnapshotsResponse>();
        Assert.NotNull(responseSnapshots);

        snapshots.Sort((a, b) => a.SavedAt.CompareTo(b.SavedAt));
        var sortedSnapshots = snapshots.TakeLast(59).ToList(); // 59 without current snapshot

        var firstSnapshot = responseSnapshots.Snapshots.FirstOrDefault();
        Assert.NotNull(firstSnapshot);
        Assert.Equal(sortedSnapshots.First().SavedAt, firstSnapshot.SavedAt);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserGrapthWithInvalidModeQuery(string mode)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={mode}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}