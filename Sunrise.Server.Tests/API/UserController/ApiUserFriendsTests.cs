﻿using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserFriendsTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestUserFriendsInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/friends?limit={limit}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestUserFriendsInvalidPage(string page)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/friends?page={page}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Fact]
    public async Task TestUserFriends()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var randomUser = _mocker.User.GetRandomUser();
        await CreateTestUser(randomUser);

        var randomUserResponse = new UserResponse(Database, Sessions, randomUser);

        user.AddFriend(randomUser.Id);
        await Database.Users.UpdateUser(user);

        // Act
        var response = await client.GetAsync("user/friends");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsync<FriendsResponse>();
        var responseUser = responseUsers?.Friends.FirstOrDefault();
        Assert.NotNull(responseUser);

        Assert.Equivalent(randomUserResponse, responseUser);
    }

    [Fact]
    public async Task TestUserFriendsPageAndLimitAttribute()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var lastAddedUserId = 0;

        for (var i = 0; i < 2; i++)
        {
            var randomUser = _mocker.User.GetRandomUser();
            randomUser.Username = $"username_{i.ToString()}";

            await CreateTestUser(randomUser);
            user.AddFriend(randomUser.Id);
            await Database.Users.UpdateUser(user);

            lastAddedUserId = randomUser.Id;
        }

        // Act
        var response = await client.GetAsync("user/friends?page=2&limit=1");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsync<FriendsResponse>();
        Assert.NotNull(responseUsers);

        Assert.Single(responseUsers.Friends);
        Assert.Equal(responseUsers.Friends.First().Id, lastAddedUserId);
        Assert.Equal(2, responseUsers.TotalCount);
    }

    [Fact]
    public async Task TestUserFriendsIgnoreRestrictedUsers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var randomUser = _mocker.User.GetRandomUser();
        await CreateTestUser(randomUser);

        user.AddFriend(randomUser.Id);
        await Database.Users.UpdateUser(user);

        await Database.Users.Moderation.RestrictPlayer(randomUser.Id, null, "Test");

        // Act
        var response = await client.GetAsync("user/friends");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsync<FriendsResponse>();
        Assert.NotNull(responseUsers);

        Assert.Empty(responseUsers.Friends);
        Assert.Equal(0, responseUsers.TotalCount);
    }
}