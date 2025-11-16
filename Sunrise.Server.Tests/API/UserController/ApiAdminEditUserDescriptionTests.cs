using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
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
public class ApiAdminEditUserDescriptionTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminEditUserDescriptionWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/edit/description", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditUserDescriptionWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/edit/description", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditUserDescriptionWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync("user/999999/edit/description",
            new StringContent("{\"description\":\"test\"}", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminEditUserDescriptionWithoutBody()
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
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/description", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestAdminEditUserDescriptionWithInvalidDescriptionLength()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newDescription = _mocker.GetRandomString(2001);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/description",
            new EditDescriptionRequest
            {
                Description = newDescription
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestAdminEditUserDescription()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var oldDescription = targetUser.Description ?? string.Empty;
        var newDescription = _mocker.GetRandomString();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/description",
            new EditDescriptionRequest
            {
                Description = newDescription
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(newDescription, updatedUser.Description);

        var (totalCount, events) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.ChangeDescription)
            });

        Assert.Equal(1, totalCount);
        var descriptionChangeEvent = events.First();
        Assert.Equal(UserEventType.ChangeDescription, descriptionChangeEvent.EventType);
        Assert.Equal(targetUser.Id, descriptionChangeEvent.UserId);

        var actualData = descriptionChangeEvent.GetData<JsonElement>();

        Assert.Equal(oldDescription, actualData.GetProperty("OldDescription").GetString());
        Assert.Equal(newDescription, actualData.GetProperty("NewDescription").GetString());
        Assert.Equal(adminUser.Id, actualData.GetProperty("UpdatedById").GetInt32());
    }

    [Fact]
    public async Task TestAdminEditUserDescriptionForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Test");

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newDescription = _mocker.GetRandomString();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/description",
            new EditDescriptionRequest
            {
                Description = newDescription
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(newDescription, updatedUser.Description);
    }
}