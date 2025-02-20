using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Extensions;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserPasswordChangeTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPasswordChangeWithoutAuthToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("user/password/change",
            new ChangePasswordRequest
            {
                CurrentPassword = _mocker.GetRandomString(),
                NewPassword = _mocker.GetRandomString()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Fact]
    public async Task TestPasswordChangeWithActiveRestriction()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        // Act
        var response = await client.PostAsJsonAsync("user/password/change",
            new ChangePasswordRequest
            {
                CurrentPassword = _mocker.GetRandomString(),
                NewPassword = _mocker.GetRandomString()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Fact]
    public async Task TestPasswordChangeWithoutBody()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/password/change", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("fields are missing", responseError?.Error);
    }

    [Theory]
    [InlineData("password", null)]
    [InlineData(null, "password")]
    public async Task TestPasswordChangeWithoutOneOfPassword(string currentPassword, string newPassword)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/password/change",
            new ChangePasswordRequest
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("fields are missing", responseError?.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("new password")]
    [InlineData("new\npassword")]
    [InlineData("テスト")]
    [InlineData("1234567890123456789012345678901234567890")]
    public async Task TestPasswordChangeWithInvalidPassword(string newPassword)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.GetRandomString();
        var user = _mocker.User.GetRandomUser();
        user.Passhash = password.GetPassHash();
        user = await CreateTestUser(user);

        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/password/change",
            new ChangePasswordRequest
            {
                CurrentPassword = password,
                NewPassword = newPassword
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var (_, expectedError) = newPassword.IsValidPassword();

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(expectedError, responseError?.Error);
    }

    [Fact]
    public async Task TestPasswordChangeWithInvalidCurrentPassword()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var tokens = await GetUserAuthTokens();
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/password/change",
            new ChangePasswordRequest
            {
                CurrentPassword = _mocker.User.GetRandomPassword() + "1",
                NewPassword = _mocker.User.GetRandomPassword()
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Current password is incorrect", responseError?.Error);
    }

    [Fact]
    public async Task TestPasswordChange()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.GetRandomString();
        var user = _mocker.User.GetRandomUser();
        user.Passhash = password.GetPassHash();
        user = await CreateTestUser(user);

        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var newPassword = _mocker.User.GetRandomPassword();

        // Act
        var response = await client.PostAsJsonAsync("user/password/change",
            new ChangePasswordRequest
            {
                CurrentPassword = password,
                NewPassword = newPassword
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var newUser = await database.UserService.GetUser(user.Id);

        Assert.Equal(newPassword.GetPassHash(), newUser.Passhash);
    }
}