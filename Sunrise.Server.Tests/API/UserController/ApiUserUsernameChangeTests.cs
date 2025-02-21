using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserUsernameChangeTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestUsernameChangeWithoutAuthToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("user/username/change",
            new UsernameChangeRequest
            {
                NewUsername = _mocker.User.GetRandomUsername()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Fact]
    public async Task TestUsernameChangeWithActiveRestriction()
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
        var response = await client.PostAsJsonAsync("user/username/change",
            new UsernameChangeRequest
            {
                NewUsername = _mocker.User.GetRandomUsername()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Fact]
    public async Task TestUsernameChangeWithoutBody()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/username/change", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("fields are missing", responseError?.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("peppy")]
    [InlineData("テスト")]
    [InlineData("username ")]
    [InlineData("user+name")]
    [InlineData("user\nname")]
    [InlineData("username_old1")]
    [InlineData("1234567890123456789012345678901234567890")]
    public async Task TestUsernameChangeWithInvalidUsername(string newUsername)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/username/change",
            new UsernameChangeRequest
            {
                NewUsername = newUsername
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var (_, expectedError) = newUsername.IsValidUsername();

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(expectedError, responseError?.Error);
    }

    [Fact]
    public async Task TestUsernameChange()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var newUsername = _mocker.User.GetRandomUsername();

        // Act
        var response = await client.PostAsJsonAsync("user/username/change",
            new UsernameChangeRequest
            {
                NewUsername = newUsername
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var updatedUser = await database.UserService.GetUser(user.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(updatedUser.Username, newUsername);
    }

    [Theory]
    [InlineData(UserAccountStatus.Restricted)]
    [InlineData(UserAccountStatus.Active)]
    [InlineData(UserAccountStatus.Disabled)]
    public async Task TestUsernameChangeWithOtherUsersUsername(UserAccountStatus status)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var otherUser = _mocker.User.GetRandomUser();
        otherUser.AccountStatus = status;
        otherUser = await CreateTestUser(otherUser);

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/username/change",
            new UsernameChangeRequest
            {
                NewUsername = otherUser.Username
            });

        // Assert
        var isUsernameChangeExpected = status == UserAccountStatus.Disabled;

        Assert.Equal(isUsernameChangeExpected ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);


        if (isUsernameChangeExpected)
        {
            var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
            var updatedOtherUser = await database.UserService.GetUser(otherUser.Id);
            Assert.NotNull(updatedOtherUser);

            Assert.Equal(updatedOtherUser.Username, otherUser.Username.SetUsernameAsOld());
        }
        else
        {
            var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.Contains("Username is already taken", responseError?.Error);
        }
    }
}