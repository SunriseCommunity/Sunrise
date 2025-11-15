using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Enums;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiUserEditFriendStatusTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
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

        var (totalCount, events) = await Database.Events.Users.GetUserEvents(user.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.ChangeFriendshipStatus)
            });

        Assert.Equal(1, totalCount);
        var friendshipStatusChangeEvent = events.First();
        Assert.Equal(user.Id, friendshipStatusChangeEvent.UserId);

        var data = friendshipStatusChangeEvent.GetData<JsonElement>();

        Assert.Equal(user.Id, data.GetProperty("UpdatedById").GetInt32());
        Assert.Equal(requestedUser.Id, data.GetProperty("TargetFriendshipUserId").GetInt32());
        Assert.Equal(isFriendsBefore ? (int)UserRelation.Friend : (int)UserRelation.None, data.GetProperty("OldRelation").GetInt32());
        Assert.Equal(isFriendsAfter ? (int)UserRelation.Friend : (int)UserRelation.None, data.GetProperty("NewRelation").GetInt32());
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