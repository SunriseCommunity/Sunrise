using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserGetUserFriendsCountTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetUserFriendsCountUserNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var userId = _mocker.GetRandomInteger(minInt: 2);

        // Act
        var response = await client.GetAsync($"user/{userId}/friends/count");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseContent?.Detail);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserFriendsCountInvalidRoute(string userId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/friends/count");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task TestGetUserFriendsCount(bool isUserFollows, bool isUserBeingFollowed)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var requestedUser = await CreateTestUser();

        if (isUserFollows)
        {
            var userRelationship = await Database.Users.Relationship.GetUserRelationship(user.Id, requestedUser.Id);
            if (userRelationship == null)
                return;

            userRelationship.Relation = UserRelation.Friend;

            await Database.Users.Relationship.UpdateUserRelationship(userRelationship);
        }

        if (isUserBeingFollowed)
        {
            var requestedUserRelationship = await Database.Users.Relationship.GetUserRelationship(requestedUser.Id, user.Id);
            if (requestedUserRelationship == null)
                return;

            requestedUserRelationship.Relation = UserRelation.Friend;

            await Database.Users.Relationship.UpdateUserRelationship(requestedUserRelationship);
        }

        // Act
        var response = await client.GetAsync($"user/{user.Id}/friends/count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<UserRelationsCountersResponse>();

        Assert.NotNull(responseContent);

        Assert.Equal(isUserFollows ? 1 : 0, responseContent.Following);
        Assert.Equal(isUserBeingFollowed ? 1 : 0, responseContent.Followers);
    }

    [Fact]
    public async Task TestGetUserFriendsCountIgnoreRestrictedUsers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var requestedUser = await CreateTestUser();

        var userRelationship = await Database.Users.Relationship.GetUserRelationship(user.Id, requestedUser.Id);
        if (userRelationship == null)
            return;

        userRelationship.Relation = UserRelation.Friend;

        await Database.Users.Relationship.UpdateUserRelationship(userRelationship);

        var requestedUserRelationship = await Database.Users.Relationship.GetUserRelationship(user.Id, requestedUser.Id);
        if (requestedUserRelationship == null)
            return;

        requestedUserRelationship.Relation = UserRelation.Friend;

        await Database.Users.Relationship.UpdateUserRelationship(requestedUserRelationship);

        await Database.Users.Moderation.RestrictPlayer(requestedUser.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/friends/count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<UserRelationsCountersResponse>();

        Assert.NotNull(responseContent);

        Assert.Equal(0, responseContent.Following);
        Assert.Equal(0, responseContent.Followers);
    }

    [Fact]
    public async Task TestGetUserFriendsCountWithActiveRestriction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/friends/count");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseContent?.Detail);
    }
}