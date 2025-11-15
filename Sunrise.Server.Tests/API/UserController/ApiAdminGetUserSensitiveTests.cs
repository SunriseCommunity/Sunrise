using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminGetUserSensitiveTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminGetUserSensitiveWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminGetUserSensitiveWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminGetUserSensitiveWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/999999/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("test")]
    public async Task TestAdminGetUserSensitiveInvalidRoute(string userId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{userId}/sensitive");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminGetUserSensitive()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var userData = new UserSensitiveResponse(Sessions, targetUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseUser = await response.Content.ReadFromJsonAsyncWithAppConfig<UserSensitiveResponse>();
        Assert.NotNull(responseUser);

        Assert.Equal(userData.Id, responseUser.Id);
        Assert.Equal(userData.Username, responseUser.Username);
        Assert.Equal(userData.Email, responseUser.Email);
        Assert.Equal(userData.Privilege, responseUser.Privilege);
        Assert.Equal(userData.Description, responseUser.Description);
        Assert.Equal(userData.Country, responseUser.Country);
        Assert.Equal(userData.IsRestricted, responseUser.IsRestricted);
        Assert.Equal(userData.DefaultGameMode, responseUser.DefaultGameMode);
    }

    [Fact]
    public async Task TestAdminGetUserSensitiveIncludesEmail()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var expectedEmail = targetUser.Email;

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseUser = await response.Content.ReadFromJsonAsyncWithAppConfig<UserSensitiveResponse>();
        Assert.NotNull(responseUser);
        Assert.Equal(expectedEmail, responseUser.Email);
    }

    [Fact]
    public async Task TestAdminGetUserSensitiveIncludesPrivilege()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = _mocker.User.GetRandomUser();
        targetUser.Privilege = UserPrivilege.Supporter;
        await CreateTestUser(targetUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseUser = await response.Content.ReadFromJsonAsyncWithAppConfig<UserSensitiveResponse>();
        Assert.NotNull(responseUser);
        Assert.Equal(JsonStringFlagEnumHelper.SplitFlags(UserPrivilege.Supporter), responseUser.Privilege);
    }

    [Fact]
    public async Task TestAdminGetUserSensitiveForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Test restriction");

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseUser = await response.Content.ReadFromJsonAsyncWithAppConfig<UserSensitiveResponse>();
        Assert.NotNull(responseUser);
        Assert.Equal(targetUser.Id, responseUser.Id);
        Assert.True(responseUser.IsRestricted);
    }

    [Fact]
    public async Task TestAdminGetUserSensitiveForSelf()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var userData = new UserSensitiveResponse(Sessions, adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act - Admin can get their own sensitive data
        var response = await client.GetAsync($"user/{adminUser.Id}/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseUser = await response.Content.ReadFromJsonAsyncWithAppConfig<UserSensitiveResponse>();
        Assert.NotNull(responseUser);
        Assert.Equal(userData.Id, responseUser.Id);
        Assert.Equal(userData.Email, responseUser.Email);
        Assert.Equal(userData.Privilege, responseUser.Privilege);
    }

    [Fact]
    public async Task TestAdminGetUserSensitiveWithSession()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var session = CreateTestSession(targetUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseUser = await response.Content.ReadFromJsonAsyncWithAppConfig<UserSensitiveResponse>();
        Assert.NotNull(responseUser);
        Assert.NotEqual("Offline", responseUser.UserStatus);
    }

    [Fact]
    public async Task TestAdminGetUserSensitiveWithoutSession()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        // No session created

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/sensitive");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseUser = await response.Content.ReadFromJsonAsyncWithAppConfig<UserSensitiveResponse>();
        Assert.NotNull(responseUser);
        Assert.Equal("Offline", responseUser.UserStatus);
    }
}