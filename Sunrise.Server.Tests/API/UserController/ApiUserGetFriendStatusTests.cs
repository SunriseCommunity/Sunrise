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
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
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
            var relationship = await Database.Users.Relationship.GetUserRelationship(user.Id, requestedUser.Id);
            if (relationship == null)
                return;

            relationship.Relation = UserRelation.Friend;

            await Database.Users.Relationship.UpdateUserRelationship(relationship);
        }

        if (isFollowingYou)
        {
            var relationship = await Database.Users.Relationship.GetUserRelationship(requestedUser.Id, user.Id);
            if (relationship == null)
                return;

            relationship.Relation = UserRelation.Friend;

            await Database.Users.Relationship.UpdateUserRelationship(relationship);
        }

        // Act
        var response = await client.GetAsync($"user/{requestedUser.Id}/friend/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<FriendStatusResponse>();
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

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }
}