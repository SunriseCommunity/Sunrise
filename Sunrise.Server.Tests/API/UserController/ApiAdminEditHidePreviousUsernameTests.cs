using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminEditHidePreviousUsernameTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminEditHidePreviousUsernameWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsync("user/edit/hide-previous-username", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditHidePreviousUsernameWithRegularUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/hide-previous-username",
            new EditHidePreviousUsernameRequest { EventId = 1, IsHidden = true });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditHidePreviousUsernameWithoutBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/hide-previous-username", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestAdminEditHidePreviousUsernameSetHiddenToTrue()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(superUser);

        var targetUser = await CreateTestUser();
        var ip = _mocker.User.GetRandomIp();
        var oldUsername = targetUser.Username;
        var newUsername = _mocker.User.GetRandomUsername();

        await Database.Events.Users.AddUserChangeUsernameEvent(new UserEventAction(targetUser, ip, targetUser.Id), oldUsername, newUsername);

        var events = await Database.Events.Users.GetUserPreviousUsernameChangeEvents(targetUser.Id);
        var changeEvent = events.First();

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/hide-previous-username",
            new EditHidePreviousUsernameRequest { EventId = changeEvent.Id, IsHidden = true });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedEvents = await Database.Events.Users.GetUserPreviousUsernameChangeEvents(targetUser.Id);
        var updatedEvent = updatedEvents.First();
        var data = updatedEvent.GetData<UserUsernameChanged>();
        Assert.NotNull(data);
        Assert.True(data.IsHiddenFromPreviousUsernames);
    }

    [Fact]
    public async Task TestAdminEditHidePreviousUsernameSetHiddenToFalse()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(superUser);

        var targetUser = await CreateTestUser();
        var ip = _mocker.User.GetRandomIp();
        var oldUsername = targetUser.Username;
        var newUsername = _mocker.User.GetRandomUsername();

        await Database.Events.Users.AddUserChangeUsernameEvent(new UserEventAction(targetUser, ip, targetUser.Id), oldUsername, newUsername);

        var events = await Database.Events.Users.GetUserPreviousUsernameChangeEvents(targetUser.Id);
        var changeEvent = events.First();

        await Database.Events.Users.SetUserChangeUsernameEventVisibility(changeEvent.Id, true);

        var eventsAfterHide = await Database.Events.Users.GetUserPreviousUsernameChangeEvents(targetUser.Id);
        var hiddenData = eventsAfterHide.First().GetData<UserUsernameChanged>();
        Assert.True(hiddenData?.IsHiddenFromPreviousUsernames);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/hide-previous-username",
            new EditHidePreviousUsernameRequest { EventId = changeEvent.Id, IsHidden = false });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedEvents = await Database.Events.Users.GetUserPreviousUsernameChangeEvents(targetUser.Id);
        var data = updatedEvents.First().GetData<UserUsernameChanged>();
        Assert.NotNull(data);
        Assert.False(data.IsHiddenFromPreviousUsernames);
    }
}
