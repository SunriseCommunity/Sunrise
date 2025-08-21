using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Enums;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
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
        var response = await client.PostAsJsonAsync($"user/{requestedUser.Id}/friend/status",
            new EditFriendshipStatusRequest
            {
                Action = UpdateFriendshipStatusAction.Add
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        var response = await client.PostAsJsonAsync($"user/{requestedUser.Id}/friend/status",
            new EditFriendshipStatusRequest
            {
                Action = UpdateFriendshipStatusAction.Add
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        var response = await client.PostAsJsonAsync($"user/{userId}/friend/status",
            new EditFriendshipStatusRequest
            {
                Action = UpdateFriendshipStatusAction.Add
            });

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
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

        var json = $"{{\"action\":\"{action}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync($"user/{requestedUser.Id}/friend/status", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
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
        var action = isFriendsAfter ? UpdateFriendshipStatusAction.Add : UpdateFriendshipStatusAction.Remove;

        var relationship = await Database.Users.Relationship.GetUserRelationship(user.Id, requestedUser.Id);
        if (relationship == null)
            return;

        if (isFriendsBefore)
        {
            relationship.Relation = UserRelation.Friend;
        }
        else
        {
            relationship.Relation = UserRelation.None;
        }

        await Database.Users.Relationship.UpdateUserRelationship(relationship);

        var result = await Database.Users.UpdateUser(user);
        if (result.IsFailure)
            throw new Exception(result.Error);

        // Act
        var response = await client.PostAsJsonAsync($"user/{requestedUser.Id}/friend/status",
            new EditFriendshipStatusRequest
            {
                Action = action
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(user.Id);
        await Database.DbContext.Entry(relationship).ReloadAsync();

        Assert.NotNull(updatedUser);
        Assert.Equal(isFriendsAfter, relationship.Relation == UserRelation.Friend);
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
        var response = await client.PostAsJsonAsync($"user/{requestedUser.Id}/friend/status",
            new EditFriendshipStatusRequest
            {
                Action = UpdateFriendshipStatusAction.Add
            });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }
}