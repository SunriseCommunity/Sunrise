using System.Net;
using System.Net.Http.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserEditFriendStatusTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestEditFriendStatusWithoutAuthToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var requestedUser = await CreateTestUser();

        // Act
        var response = await client.PostAsync($"user/{requestedUser.Id}/friend/status", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Fact]
    public async Task TestEditFriendStatusWithActiveRestriction()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        var requestedUser = await CreateTestUser();

        // Act
        var response = await client.PostAsync($"user/{requestedUser.Id}/friend/status", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestEditFriendStatusWithInvalidUserId(string userId)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync($"user/{userId}/friend/status", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestEditFriendStatusWithInvalidAction()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var requestedUser = await CreateTestUser();
        var action = _mocker.GetRandomString();

        // Act
        var response = await client.PostAsync($"user/{requestedUser.Id}/friend/status?action={action}", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("action parameter", responseError?.Error);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task TestEditFriendStatus(bool isFriendsBefore, bool isFriendsAfter)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var requestedUser = await CreateTestUser();
        var action = isFriendsAfter ? "add" : "remove";

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        if (isFriendsBefore)
        {
            user.AddFriend(requestedUser.Id);
        }
        else
        {
            user.RemoveFriend(requestedUser.Id);
        }

        await database.UserService.UpdateUser(user);

        // Act
        var response = await client.PostAsync($"user/{requestedUser.Id}/friend/status?action={action}", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await database.UserService.GetUser(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(isFriendsAfter, updatedUser.FriendsList.Contains(requestedUser.Id));
    }

    [Fact]
    public async Task TestGetFriendStatusWithoutAuthToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var requestedUser = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{requestedUser.Id}/friend/status");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Fact]
    public async Task TestGetFriendStatusWithActiveRestriction()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        var requestedUser = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{requestedUser.Id}/friend/status");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetFriendStatusWithInvalidUserId(string userId)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{userId}/friend/status");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task TestGetFriendStatus(bool isFollowingYou, bool isFollowedByYou)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var requestedUser = await CreateTestUser();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        if (isFollowedByYou)
        {
            user.AddFriend(requestedUser.Id);
            await database.UserService.UpdateUser(user);
        }

        if (isFollowingYou)
        {
            requestedUser.AddFriend(user.Id);
            await database.UserService.UpdateUser(requestedUser);
        }

        // Act
        var response = await client.GetAsync($"user/{requestedUser.Id}/friend/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<FriendStatusResponse>();
        Assert.NotNull(responseContent);

        Assert.Equal(isFollowingYou, responseContent.IsFollowingYou);
        Assert.Equal(isFollowedByYou, responseContent.IsFollowedByYou);
    }
}