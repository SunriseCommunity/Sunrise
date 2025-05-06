using System.Net;
using System.Text.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Utils;

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
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("user/self");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetSelfWithActiveRestriction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync("user/self");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetSelf()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var userData = new UserResponse(Sessions, user);

        // Act
        var response = await client.GetAsync("user/self");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUser = await response.Content.ReadFromJsonAsyncWithAppConfig<UserResponse>();
        Assert.NotNull(responseUser);

        Assert.Equivalent(userData, responseUser);
    }

    [Fact]
    public async Task TestGetSelfWithStatus()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var session = CreateTestSession(user);

        // Act
        var response = await client.GetAsync("user/self");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUser = await response.Content.ReadFromJsonAsyncWithAppConfig<UserResponse>();
        Assert.NotNull(responseUser);

        Assert.Equal(session.Attributes.Status.ToText(), responseUser.UserStatus);
        Assert.Equal(session.Attributes.LastPingRequest, responseUser.LastOnlineTime);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestGetSelfWithUserStats(GameMode mode)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/self/{mode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var jsonDoc = await response.Content.ReadFromJsonAsyncWithAppConfig<JsonDocument>();
        var responseStats = jsonDoc?.RootElement.GetProperty("stats").Deserialize<UserStatsResponse>(Configuration.SystemTextJsonOptions);

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
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync($"user/self/{mode}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}