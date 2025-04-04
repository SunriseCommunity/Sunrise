﻿using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserEditFriendStatusTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestEditFriendStatusWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

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
        var client = App.CreateClient().UseClient("api");

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
        var client = App.CreateClient().UseClient("api");

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
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var requestedUser = await CreateTestUser();
        var action = isFriendsAfter ? "add" : "remove";

        if (isFriendsBefore)
        {
            user.AddFriend(requestedUser.Id);
        }
        else
        {
            user.RemoveFriend(requestedUser.Id);
        }

        var result = await Database.Users.UpdateUser(user);
        if (result.IsFailure)
            throw new Exception(result.Error);


        // Act
        var response = await client.PostAsync($"user/{requestedUser.Id}/friend/status?action={action}", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(user.Id);
        await Database.DbContext.Entry(updatedUser!).ReloadAsync();

        Assert.NotNull(updatedUser);
        Assert.Equal(isFriendsAfter, updatedUser.FriendsList.Contains(requestedUser.Id));
    }

    [Fact]
    public async Task TestEditFriendStatusForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var requestedUser = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(requestedUser.Id, null, "Test");

        // Act
        var response = await client.PostAsync($"user/{requestedUser.Id}/friend/status?action=add", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("User not found", responseError?.Error);
    }
}