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
public class ApiAdminGetUserFollowersTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminGetUserFollowersWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/followers");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminGetUserFollowersWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/followers");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminGetUserFollowersWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/999999/followers");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestAdminGetUserFollowersInvalidLimit(string limit)
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
        var response = await client.GetAsync($"user/{targetUser.Id}/followers?limit={limit}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestAdminGetUserFollowersInvalidPage(string page)
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
        var response = await client.GetAsync($"user/{targetUser.Id}/followers?page={page}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminGetUserFollowers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var followerUser = _mocker.User.GetRandomUser();
        await CreateTestUser(followerUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Create a relationship - followerUser follows targetUser
        var relationship = await Database.Users.Relationship.GetUserRelationship(followerUser.Id, targetUser.Id);
        if (relationship == null)
            return;

        relationship.Relation = UserRelation.Friend; // Followers are typically friends
        await Database.Users.Relationship.UpdateUserRelationship(relationship);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/followers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<FollowersResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Followers);
        Assert.Single(responseData.Followers);
        Assert.Equal(1, responseData.TotalCount);
        Assert.Equal(followerUser.Id, responseData.Followers.First().Id);
    }

    [Fact]
    public async Task TestAdminGetUserFollowersWithPagination()
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
            var followerUser = _mocker.User.GetRandomUser();
            followerUser.Username = $"follower_{i}";
            await CreateTestUser(followerUser);

            var relationship = await Database.Users.Relationship.GetUserRelationship(followerUser.Id, targetUser.Id);
            if (relationship == null)
                continue;

            relationship.Relation = UserRelation.Friend;
            await Database.Users.Relationship.UpdateUserRelationship(relationship);

            lastAddedUserId = followerUser.Id;
        }

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/followers?page=2&limit=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<FollowersResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Followers);
        Assert.Single(responseData.Followers);
        Assert.Equal(3, responseData.TotalCount);
        Assert.Equal(lastAddedUserId, responseData.Followers.First().Id);
    }

    [Fact]
    public async Task TestAdminGetUserFollowersEmptyList()
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
        var response = await client.GetAsync($"user/{targetUser.Id}/followers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<FollowersResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Followers);
        Assert.Empty(responseData.Followers);
        Assert.Equal(0, responseData.TotalCount);
    }

    [Fact]
    public async Task TestAdminGetUserFollowersIgnoresRestrictedUsers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var followerUser = _mocker.User.GetRandomUser();
        await CreateTestUser(followerUser);

        var relationship = await Database.Users.Relationship.GetUserRelationship(followerUser.Id, targetUser.Id);
        if (relationship == null)
            return;

        relationship.Relation = UserRelation.Friend;
        await Database.Users.Relationship.UpdateUserRelationship(relationship);

        await Database.Users.Moderation.RestrictPlayer(followerUser.Id, null, "Test");

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/followers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<FollowersResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Followers);
        Assert.Empty(responseData.Followers);
        Assert.Equal(0, responseData.TotalCount);
    }
}

