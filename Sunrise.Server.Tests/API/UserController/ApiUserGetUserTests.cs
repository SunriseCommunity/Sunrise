using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserGetUserTests : ApiTest
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
    public async Task TestGetUserNotFound()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var userId = _mocker.GetRandomInteger();

        // Act
        var response = await client.GetAsync($"user/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("User not found", responseContent?.Error);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserInvalidRoute(string userId)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUser()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var userData = new UserResponse(user);

        // Act
        var response = await client.GetAsync($"user/{user.Id}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUser = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(responseUser);
        responseUser.UserStatus = null; // Ignore user status for comparison

        Assert.Equivalent(userData, responseUser);
    }

    [Fact]
    public async Task TestGetUserWithStatus()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var session = CreateTestSession(user);

        // Act
        var response = await client.GetAsync($"user/{user.Id}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUser = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(responseUser);

        Assert.Equal(session.Attributes.Status.ToText(), responseUser.UserStatus);
        Assert.Equal(session.Attributes.LastPingRequest, responseUser.LastOnlineTime);
    }

    [Fact]
    public async Task TestGetUserRestrictedNotFound()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("User is restricted", responseContent?.Error);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestGetUserWithUserStats(GameMode mode)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        await database.ScoreService.InsertScore(score);

        // Act
        var response = await client.GetAsync($"user/{user.Id}?mode={(int)mode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var responseStats = jsonDoc?.RootElement.GetProperty("stats").Deserialize<UserStatsResponse>();

        Assert.NotNull(responseStats);
        Assert.Equal(responseStats.GameMode, mode);
        Assert.Equal(responseStats.UserId, user.Id);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserWithUserStatsInvalidModeQuery(string mode)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}?mode={mode}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}