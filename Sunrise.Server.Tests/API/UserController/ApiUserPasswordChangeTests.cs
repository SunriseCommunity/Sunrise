using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserPasswordChangeTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPasswordChangeWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("user/password/change",
            new ChangePasswordRequest
            {
                CurrentPassword = _mocker.GetRandomString(),
                NewPassword = _mocker.GetRandomString()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Contains("authorize to access", responseError?.Error);
    }

    [Fact]
    public async Task TestPasswordChangeWithActiveRestriction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.PostAsJsonAsync("user/password/change",
            new ChangePasswordRequest
            {
                CurrentPassword = _mocker.GetRandomString(),
                NewPassword = _mocker.GetRandomString()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Contains("authorize to access", responseError?.Error);
    }

    [Fact]
    public async Task TestPasswordChangeWithoutBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/password/change", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Contains("fields are missing", responseError?.Error);
    }

    [Theory]
    [InlineData("password", null)]
    [InlineData(null, "password")]
    public async Task TestPasswordChangeWithoutOneOfPassword(string currentPassword, string newPassword)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
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
        var client = App.CreateClient().UseClient("api");

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

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Equal(expectedError, responseError?.Error);
    }

    [Fact]
    public async Task TestPasswordChangeWithInvalidCurrentPassword()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ErrorResponse>();
        Assert.Equal("Current password is incorrect", responseError?.Error);
    }

    [Fact]
    public async Task TestPasswordChange()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var newUser = await Database.Users.GetUser(user.Id);

        Assert.Equal(newPassword.GetPassHash(), newUser.Passhash);
    }
}