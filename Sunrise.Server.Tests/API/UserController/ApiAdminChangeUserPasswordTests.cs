using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminChangeUserPasswordTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminChangeUserPasswordWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/password/change",
            new ResetPasswordRequest
            {
                NewPassword = _mocker.User.GetRandomPassword()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminChangeUserPasswordWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/password/change",
            new ResetPasswordRequest
            {
                NewPassword = _mocker.User.GetRandomPassword()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminChangeUserPasswordWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/999999/password/change",
            new ResetPasswordRequest
            {
                NewPassword = _mocker.User.GetRandomPassword()
            });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminChangeUserPasswordWithoutBody()
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
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/password/change", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestAdminChangeUserPassword()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var oldPasshash = targetUser.Passhash;

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newPassword = _mocker.User.GetRandomPassword();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/password/change",
            new ResetPasswordRequest
            {
                NewPassword = newPassword
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(newPassword.GetPassHash(), updatedUser.Passhash);
        Assert.NotEqual(oldPasshash, updatedUser.Passhash);
    }

    [Fact]
    public async Task TestAdminChangeUserPasswordForRestrictedUser()
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

        var newPassword = _mocker.User.GetRandomPassword();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/password/change",
            new ResetPasswordRequest
            {
                NewPassword = newPassword
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(newPassword.GetPassHash(), updatedUser.Passhash);
    }

    [Fact]
    public async Task TestAdminChangeUserPasswordLogsEvent()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var oldPasshash = targetUser.Passhash;

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newPassword = _mocker.User.GetRandomPassword();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/password/change",
            new ResetPasswordRequest
            {
                NewPassword = newPassword
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify password was changed
        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(newPassword.GetPassHash(), updatedUser.Passhash);
        Assert.NotEqual(oldPasshash, updatedUser.Passhash);
    }
}
