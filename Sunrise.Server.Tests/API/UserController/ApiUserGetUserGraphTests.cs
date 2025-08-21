using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

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
        var client = App.CreateClient().UseClient("api");

        var userId = _mocker.GetRandomInteger();
        var gamemode = _mocker.Score.GetRandomGameMode();

        // Act
        var response = await client.GetAsync($"user/{userId}/graph?mode={(int)gamemode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseContent?.Detail);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserGraphInvalidRoute(string userId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/graph");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }


    [Fact]
    public async Task TestGetUserGraphUserRestrictedGraphNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var gamemode = _mocker.Score.GetRandomGameMode();

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={(int)gamemode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserIsRestricted, responseContent?.Detail);
    }

    [Fact]
    public async Task TestGetUserGraph()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var gamemode = _mocker.Score.GetRandomGameMode();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={(int)gamemode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseSnapshots = await response.Content.ReadFromJsonAsyncWithAppConfig<StatsSnapshotsResponse>();
        Assert.NotNull(responseSnapshots);

        var (userRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(user, gamemode);

        Assert.Equal(userRank, responseSnapshots.Snapshots.FirstOrDefault()?.Rank);
    }

    [Fact]
    public async Task TestGetUserGraphCheckSorting()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var gamemode = _mocker.Score.GetRandomGameMode();

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
        await Database.Users.Stats.Snapshots.AddUserStatsSnapshot(userStatsSnapshot);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={(int)gamemode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseSnapshots = await response.Content.ReadFromJsonAsyncWithAppConfig<StatsSnapshotsResponse>();
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
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var gamemode = _mocker.Score.GetRandomGameMode();

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
        await Database.Users.Stats.Snapshots.AddUserStatsSnapshot(userStatsSnapshot);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={(int)gamemode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseSnapshots = await response.Content.ReadFromJsonAsyncWithAppConfig<StatsSnapshotsResponse>();
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
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/graph?mode={mode}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}