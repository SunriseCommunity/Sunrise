using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserGetUserPreviousUsernamesTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetUserPreviousUsernamesUserNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var userId = _mocker.GetRandomInteger();

        // Act
        var response = await client.GetAsync($"user/{userId}/previous-usernames");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseContent?.Detail);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserPreviousUsernameInvalidRoute(string userId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/previous-usernames");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUserPreviousUsernames()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var originalUsername = user.Username;

        var arrangeUserChangeUsernameResult = await Database.Users.UpdateUserUsername(user, user.Username, _mocker.GetRandomString());
        if (arrangeUserChangeUsernameResult.IsFailure)
            throw new Exception(arrangeUserChangeUsernameResult.Error);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/previous-usernames");

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePreviousUsernames = await response.Content.ReadFromJsonAsyncWithAppConfig<PreviousUsernamesResponse>();
        Assert.NotNull(responsePreviousUsernames);

        Assert.Equal(responsePreviousUsernames.Usernames, [originalUsername]);
    }

    [Fact]
    public async Task TestGetUserPreviousUsernamesShowOnlyRecent()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        for (var i = 0; i < 10; i++)
        {
            var arrangeUserChangeUsernameResult = await Database.Users.UpdateUserUsername(user, user.Username, i.ToString());
            if (arrangeUserChangeUsernameResult.IsFailure)
                throw new Exception(arrangeUserChangeUsernameResult.Error);
        }

        // Act
        var response = await client.GetAsync($"user/{user.Id}/previous-usernames");

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePreviousUsernames = await response.Content.ReadFromJsonAsyncWithAppConfig<PreviousUsernamesResponse>();
        Assert.NotNull(responsePreviousUsernames);

        Assert.Equal(responsePreviousUsernames.Usernames, ["8", "7", "6"]);
    }

    [Fact]
    public async Task TestGetUserPreviousUsernamesIgnoreFilteredUsernames()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var badUsername = "badUsername";

        var arrangeUserChangeToBadUsernameResult = await Database.Users.UpdateUserUsername(user, user.Username, badUsername);
        if (arrangeUserChangeToBadUsernameResult.IsFailure)
            throw new Exception(arrangeUserChangeToBadUsernameResult.Error);

        var arrangeUserFilterUsernameResult = await Database.Users.UpdateUserUsername(user, badUsername, $"filtered_{user.Id}");
        if (arrangeUserFilterUsernameResult.IsFailure)
            throw new Exception(arrangeUserFilterUsernameResult.Error);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/previous-usernames");

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePreviousUsernames = await response.Content.ReadFromJsonAsyncWithAppConfig<PreviousUsernamesResponse>();
        Assert.NotNull(responsePreviousUsernames);

        Assert.DoesNotContain(badUsername, responsePreviousUsernames.Usernames);
    }

    [Fact]
    public async Task TestGetUserPreviousUsernamesIgnoreHiddenUsernames()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var oldUsername = "oldUsername";

        var arrangeUserChangeToBadUsernameResult = await Database.Users.UpdateUserUsername(user, user.Username, oldUsername);
        if (arrangeUserChangeToBadUsernameResult.IsFailure)
            throw new Exception(arrangeUserChangeToBadUsernameResult.Error);

        var arrangeUserFilterUsernameResult = await Database.Users.UpdateUserUsername(user, oldUsername, "newUsername");
        if (arrangeUserFilterUsernameResult.IsFailure)
            throw new Exception(arrangeUserFilterUsernameResult.Error);

        var lastUsernameChange = await Database.Events.Users.GetLastUsernameChangeEvent(user.Id);
        if (lastUsernameChange is null)
            throw new Exception("Last username change event is null");

        var changeUsernameEventVisibilityResult = await Database.Events.Users.SetUserChangeUsernameEventVisibility(lastUsernameChange.Id, true);
        if (changeUsernameEventVisibilityResult.IsFailure)
            throw new Exception(changeUsernameEventVisibilityResult.Error);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/previous-usernames");

        // Assert
        response.EnsureSuccessStatusCode();

        var responsePreviousUsernames = await response.Content.ReadFromJsonAsyncWithAppConfig<PreviousUsernamesResponse>();
        Assert.NotNull(responsePreviousUsernames);

        Assert.DoesNotContain(oldUsername, responsePreviousUsernames.Usernames);
    }

    [Fact]
    public async Task TestGetUserPreviousUsernameRestrictedUserNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/previous-usernames");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserIsRestricted, responseContent?.Detail);
    }
}