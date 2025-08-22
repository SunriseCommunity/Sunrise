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

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserUsernameChangeTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestUsernameChangeWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("user/username/change",
            new UsernameChangeRequest
            {
                NewUsername = _mocker.User.GetRandomUsername()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestUsernameChangeWithActiveRestriction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.PostAsJsonAsync("user/username/change",
            new UsernameChangeRequest
            {
                NewUsername = _mocker.User.GetRandomUsername()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestUsernameChangeWithoutBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/username/change", new StringContent(""));

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
    public async Task TestUsernameChangeWithInvalidUsername(string newUsername)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Equal(expectedError, responseError?.Detail);
    }

    [Fact]
    public async Task TestUsernameChange()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var updatedUser = await Database.Users.GetUser(user.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(updatedUser.Username, newUsername);
    }

    [Fact]
    public async Task TestUsernameChangeTooFrequently()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var usernameChangeResult = await Database.Users.UpdateUserUsername(user, user.Username, "test");
        if (usernameChangeResult.IsFailure)
            throw new Exception(usernameChangeResult.Error);

        var lastUsernameChange = await Database.Events.Users.GetLastUsernameChangeEvent(user.Id);
        Assert.NotNull(lastUsernameChange);

        var newUsername = _mocker.User.GetRandomUsername();

        // Act
        var response = await client.PostAsJsonAsync("user/username/change",
            new UsernameChangeRequest
            {
                NewUsername = newUsername
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);


        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.ChangeUsernameOnCooldown(lastUsernameChange.Time.AddDays(Configuration.UsernameChangeCooldownInDays)), responseError?.Detail);
    }

    [Theory]
    [InlineData(UserAccountStatus.Restricted)]
    [InlineData(UserAccountStatus.Active)]
    [InlineData(UserAccountStatus.Disabled)]
    public async Task TestUsernameChangeWithOtherUsersUsername(UserAccountStatus status)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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
}