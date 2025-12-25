using System.Net;
using System.Net.Http.Json;
using System.Text;
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
public class ApiAdminEditUserRestrictionTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminEditUserRestrictionWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/edit/restriction", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditUserRestrictionWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/edit/restriction", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditUserRestrictionWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync("user/999999/edit/restriction",
            new StringContent("{\"is_restrict\":true,\"restriction_reason\":\"test\"}", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminEditUserRestrictionWithoutBody()
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
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestAdminRestrictUserWithoutReason()
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
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = null
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.RestrictionReasonMustBeProvided, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminRestrictUserWithEmptyReason()
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
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = "   "
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.RestrictionReasonMustBeProvided, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminRestrictUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var isRestrictedBefore = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.False(isRestrictedBefore);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = "Test restriction reason"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var isRestrictedAfter = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.True(isRestrictedAfter);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserAccountStatus.Restricted, updatedUser.AccountStatus);

        var (_, restrictEvents) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.Restrict)
            });
        Assert.NotEmpty(restrictEvents);
        var restrictEvent = restrictEvents.First();
        Assert.Equal(UserEventType.Restrict, restrictEvent.EventType);
        Assert.Contains("Test restriction reason", restrictEvent.JsonData);
        Assert.Contains(adminUser.Id.ToString(), restrictEvent.JsonData);
    }

    [Fact]
    public async Task TestAdminRestrictUserAlreadyRestricted()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Previous restriction");

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = "New restriction reason"
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserAlreadyRestricted, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminUnrestrictUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Test restriction");

        var isRestrictedBefore = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.True(isRestrictedBefore);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = false,
                RestrictionReason = null
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var isRestrictedAfter = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.False(isRestrictedAfter);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.NotEqual(UserAccountStatus.Restricted, updatedUser.AccountStatus);

        var (_, unrestrictEvents) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.Unrestrict)
            });
        Assert.NotEmpty(unrestrictEvents);
        var unrestrictEvent = unrestrictEvents.First();
        Assert.Equal(UserEventType.Unrestrict, unrestrictEvent.EventType);
        Assert.Contains(adminUser.Id.ToString(), unrestrictEvent.JsonData);
    }

    [Fact]
    public async Task TestAdminUnrestrictUserNotRestricted()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var isRestrictedBefore = await Database.Users.Moderation.IsUserRestricted(targetUser.Id);
        Assert.False(isRestrictedBefore);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = false,
                RestrictionReason = null
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserAlreadyUnrestricted, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminRestrictAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetAdminUser = _mocker.User.GetRandomUser();
        targetAdminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(targetAdminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetAdminUser.Id}/edit/restriction",
            new EditUserRestrictionRequest
            {
                IsRestrict = true,
                RestrictionReason = "Try to restrict admin"
            });

        // Assert
        // Admin users cannot be restricted
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
