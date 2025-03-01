using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserGetFriendStatusTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetFriendStatusWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

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
        var client = App.CreateClient().UseClient("api");

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
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var requestedUser = await CreateTestUser();

        if (isFollowedByYou)
        {
            user.AddFriend(requestedUser.Id);
            await Database.Users.UpdateUser(user);
        }

        if (isFollowingYou)
        {
            requestedUser.AddFriend(user.Id);
            await Database.Users.UpdateUser(requestedUser);
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

    [Fact]
    public async Task TestGetFriendStatusForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var requestedUser = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(requestedUser.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{requestedUser.Id}/friend/status");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("User not found", responseError?.Error);
    }
}