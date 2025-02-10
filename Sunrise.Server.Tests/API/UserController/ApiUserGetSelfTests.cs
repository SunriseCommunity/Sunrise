using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserGetSelfTests : ApiTest
{
    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Select(mode => new object[]
        {
            mode
        });
    }

    [Fact]
    public async Task TestGetSelfWithoutAuthToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("user/self");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetSelfWithActiveRestriction()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        // Act
        var response = await client.GetAsync("user/self");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetSelf()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var userData = new UserResponse(user);

        // Act
        var response = await client.GetAsync("user/self");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUser = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(responseUser);
        responseUser.UserStatus = null; // Ignore user status for comparison

        Assert.Equivalent(userData, responseUser);
    }

    [Fact]
    public async Task TestGetSelfWithStatus()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var session = CreateTestSession(user);

        // Act
        var response = await client.GetAsync("user/self");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUser = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(responseUser);

        Assert.Equal(session.Attributes.Status.ToText(), responseUser.UserStatus);
        Assert.Equal(session.Attributes.LastPingRequest, responseUser.LastOnlineTime);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestGetSelfWithUserStats(GameMode mode)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/self?mode={(int)mode}");

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
    public async Task TestGetSelfWithUserStatsInvalidModeQuery(string mode)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync($"user/self?mode={mode}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}