using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserFollowersTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestUserFollowersInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/followers?limit={limit}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestUserFollowersInvalidPage(string page)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/followers?page={page}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestUserFollowers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var randomUser = _mocker.User.GetRandomUser();
        await CreateTestUser(randomUser);

        var randomUserResponse = new UserResponse(Sessions, randomUser);

        var relationship = await Database.Users.Relationship.GetUserRelationship(randomUser.Id, user.Id);
        if (relationship == null)
            return;

        relationship.Relation = UserRelation.Friend;

        await Database.Users.Relationship.UpdateUserRelationship(relationship);

        // Act
        var response = await client.GetAsync("user/followers");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsyncWithAppConfig<FollowersResponse>();
        var responseUser = responseUsers?.Followers.FirstOrDefault();
        Assert.NotNull(responseUser);

        Assert.Equivalent(randomUserResponse, responseUser);
    }

    [Fact]
    public async Task TestUserFollowersPageAndLimitAttribute()
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

            var relationship = await Database.Users.Relationship.GetUserRelationship(randomUser.Id, user.Id);
            if (relationship == null)
                return;

            relationship.Relation = UserRelation.Friend;

            await Database.Users.Relationship.UpdateUserRelationship(relationship);

            lastAddedUserId = randomUser.Id;
        }

        // Act
        var response = await client.GetAsync("user/followers?page=2&limit=1");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsyncWithAppConfig<FollowersResponse>();
        Assert.NotNull(responseUsers);

        Assert.Single(responseUsers.Followers);
        Assert.Equal(responseUsers.Followers.First().Id, lastAddedUserId);
        Assert.Equal(2, responseUsers.TotalCount);
    }

    [Fact]
    public async Task TestUserFollowersIgnoreRestrictedUsers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var randomUser = _mocker.User.GetRandomUser();
        await CreateTestUser(randomUser);

        var relationship = await Database.Users.Relationship.GetUserRelationship(randomUser.Id, user.Id);
        if (relationship == null)
            return;

        relationship.Relation = UserRelation.Friend;

        await Database.Users.Relationship.UpdateUserRelationship(relationship);

        await Database.Users.Moderation.RestrictPlayer(randomUser.Id, null, "Test");

        // Act
        var response = await client.GetAsync("user/followers");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsyncWithAppConfig<FollowersResponse>();
        Assert.NotNull(responseUsers);

        Assert.Empty(responseUsers.Followers);
        Assert.Equal(0, responseUsers.TotalCount);
    }
}