using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using Sunrise.Tests;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminChangeUserUsernameTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminChangeUserUsernameWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/username/change",
            new UsernameChangeRequest
            {
                NewUsername = _mocker.User.GetRandomUsername()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminChangeUserUsernameWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/username/change",
            new UsernameChangeRequest
            {
                NewUsername = _mocker.User.GetRandomUsername()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminChangeUserUsernameWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/999999/username/change",
            new UsernameChangeRequest
            {
                NewUsername = _mocker.User.GetRandomUsername()
            });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminChangeUserUsernameWithoutBody()
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
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/username/change", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("peppy")]
    [InlineData("テスト")]
    [InlineData("username ")]
    [InlineData("user+name")]
    [InlineData("user\nname")]
    [InlineData("username_old1")]
    [InlineData("1234567890123456789012345678901234567890")]
    public async Task TestAdminChangeUserUsernameWithInvalidUsername(string newUsername)
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
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/username/change",
            new UsernameChangeRequest
            {
                NewUsername = newUsername
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var (_, expectedError) = newUsername.IsValidUsername();

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Equal(expectedError, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminChangeUserUsername()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newUsername = _mocker.User.GetRandomUsername();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/username/change",
            new UsernameChangeRequest
            {
                NewUsername = newUsername
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(updatedUser.Username, newUsername);
    }

    [Fact]
    public async Task TestAdminChangeUserUsernameSkipsCooldown()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        // Change username once to trigger cooldown
        var usernameChangeResult = await Database.Users.UpdateUserUsername(targetUser, targetUser.Username, "test");
        if (usernameChangeResult.IsFailure)
            throw new Exception(usernameChangeResult.Error);

        var lastUsernameChange = await Database.Events.Users.GetLastUsernameChangeEvent(targetUser.Id);
        Assert.NotNull(lastUsernameChange);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newUsername = _mocker.User.GetRandomUsername();

        // Act - Admin should be able to change username even if cooldown is active
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/username/change",
            new UsernameChangeRequest
            {
                NewUsername = newUsername
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(updatedUser.Username, newUsername);
    }

    [Theory]
    [InlineData(UserAccountStatus.Restricted)]
    [InlineData(UserAccountStatus.Active)]
    [InlineData(UserAccountStatus.Disabled)]
    public async Task TestAdminChangeUserUsernameWithOtherUsersUsername(UserAccountStatus status)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var otherUser = _mocker.User.GetRandomUser();
        otherUser.AccountStatus = status;
        otherUser = await CreateTestUser(otherUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/username/change",
            new UsernameChangeRequest
            {
                NewUsername = otherUser.Username
            });

        // Assert
        var isUsernameChangeExpected = status == UserAccountStatus.Disabled;

        Assert.Equal(isUsernameChangeExpected ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);

        if (isUsernameChangeExpected)
        {
            var updatedTargetUser = await Database.Users.GetUser(targetUser.Id);
            Assert.NotNull(updatedTargetUser);
            Assert.Equal(updatedTargetUser.Username, otherUser.Username);

            var updatedOtherUser = await Database.Users.GetUser(otherUser.Id);
            Assert.NotNull(updatedOtherUser);
            Assert.Equal(updatedOtherUser.Username, otherUser.Username.SetUsernameAsOld());
        }
        else
        {
            var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
            Assert.Contains(ApiErrorResponse.Detail.UsernameAlreadyTaken, responseError?.Detail);
        }
    }

    [Fact]
    public async Task TestAdminChangeUserUsernameForRestrictedUser()
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

        var newUsername = _mocker.User.GetRandomUsername();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/username/change",
            new UsernameChangeRequest
            {
                NewUsername = newUsername
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(updatedUser.Username, newUsername);
    }
}

