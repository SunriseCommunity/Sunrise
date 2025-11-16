using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using Sunrise.Tests;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminGetUserFriendsTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminGetUserFriendsWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/friends");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminGetUserFriendsWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/friends");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminGetUserFriendsWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/999999/friends");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestAdminGetUserFriendsInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/friends?limit={limit}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestAdminGetUserFriendsInvalidPage(string page)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/friends?page={page}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminGetUserFriends()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var friendUser = _mocker.User.GetRandomUser();
        await CreateTestUser(friendUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var relationship = await Database.Users.Relationship.GetUserRelationship(targetUser.Id, friendUser.Id);
        if (relationship == null)
            return;

        relationship.Relation = UserRelation.Friend;
        await Database.Users.Relationship.UpdateUserRelationship(relationship);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/friends");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<FriendsResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Friends);
        Assert.Single(responseData.Friends);
        Assert.Equal(1, responseData.TotalCount);
        Assert.Equal(friendUser.Id, responseData.Friends.First().Id);
    }

    [Fact]
    public async Task TestAdminGetUserFriendsWithPagination()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var lastAddedUserId = 0;

        for (var i = 0; i < 3; i++)
        {
            var friendUser = _mocker.User.GetRandomUser();
            friendUser.Username = $"friend_{i}";
            await CreateTestUser(friendUser);

            var relationship = await Database.Users.Relationship.GetUserRelationship(targetUser.Id, friendUser.Id);
            if (relationship == null)
                continue;

            relationship.Relation = UserRelation.Friend;
            await Database.Users.Relationship.UpdateUserRelationship(relationship);

            lastAddedUserId = friendUser.Id;
        }

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/friends?page=2&limit=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<FriendsResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Friends);
        Assert.Single(responseData.Friends);
        Assert.Equal(3, responseData.TotalCount);
        Assert.Equal(lastAddedUserId, responseData.Friends.First().Id);
    }

    [Fact]
    public async Task TestAdminGetUserFriendsEmptyList()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/friends");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<FriendsResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Friends);
        Assert.Empty(responseData.Friends);
        Assert.Equal(0, responseData.TotalCount);
    }

    [Fact]
    public async Task TestAdminGetUserFriendsIgnoresRestrictedUsers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var friendUser = _mocker.User.GetRandomUser();
        await CreateTestUser(friendUser);

        var relationship = await Database.Users.Relationship.GetUserRelationship(targetUser.Id, friendUser.Id);
        if (relationship == null)
            return;

        relationship.Relation = UserRelation.Friend;
        await Database.Users.Relationship.UpdateUserRelationship(relationship);

        await Database.Users.Moderation.RestrictPlayer(friendUser.Id, null, "Test");

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/friends");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<FriendsResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Friends);
        Assert.Empty(responseData.Friends);
        Assert.Equal(0, responseData.TotalCount);
    }
}

