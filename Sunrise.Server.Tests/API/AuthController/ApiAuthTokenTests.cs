using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.AuthController;

public class ApiAuthTokenTests : ApiTest
{
    private readonly MockService _mocker = new();

    private static string BannedIp => Configuration.BannedIps.FirstOrDefault() ?? throw new Exception("Banned IP not found");

    [Fact]
    public async Task TestGetUserAuthTokens()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var user = await CreateTestUser(new User
        {
            Username = "user",
            Email = "user@mail.com",
            Passhash = password.GetPassHash(),
            Country = _mocker.User.GetRandomCountryCode()
        });

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new TokenRequest
            {
                Username = user.Username,
                Password = password
            });

        // Assert
        response.EnsureSuccessStatusCode();

        var responseTokens = await response.Content.ReadFromJsonAsyncWithAppConfig<TokenResponse>();

        Assert.NotNull(responseTokens);
        Assert.NotNull(responseTokens.Token);
        Assert.NotNull(responseTokens.RefreshToken);

        Assert.True(responseTokens.ExpiresIn > 0);
    }

    [Fact]
    public async Task TestGetUserAuthTokensIgnoreUsernameCasing()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var user = await CreateTestUser(new User
        {
            Username = "User",
            Email = "user@mail.com",
            Passhash = password.GetPassHash(),
            Country = _mocker.User.GetRandomCountryCode()
        });

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new TokenRequest
            {
                Username = user.Username.ToUpper(),
                Password = password
            });

        // Assert
        response.EnsureSuccessStatusCode();

        var responseTokens = await response.Content.ReadFromJsonAsyncWithAppConfig<TokenResponse>();

        Assert.NotNull(responseTokens);
        Assert.NotNull(responseTokens.Token);
        Assert.NotNull(responseTokens.RefreshToken);

        Assert.True(responseTokens.ExpiresIn > 0);
    }

    [Fact]
    public async Task TestGetUserAuthTokensMissingBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new
            {
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseTokens?.Title);
    }

    [Fact]
    public async Task TestGetInvalidUserAuthTokens()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new TokenRequest
            {
                Username = "invalid",
                Password = "invalid"
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InvalidCredentialsProvided, responseTokens?.Detail);
    }

    [Fact]
    public async Task TestGetUserAuthTokensInvalidPassword()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new TokenRequest
            {
                Username = user.Username,
                Password = "invalid"
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InvalidCredentialsProvided, responseTokens?.Detail);
    }

    [Fact]
    public async Task TestGetRestrictedUserInvalidAuthTokens()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var user = await CreateTestUser(new User
        {
            Username = "user",
            Email = "user@mail.com",
            Passhash = password.GetPassHash(),
            Country = _mocker.User.GetRandomCountryCode()
        });

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new TokenRequest
            {
                Username = user.Username,
                Password = password
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();

        Assert.Contains(ApiErrorResponse.Detail.YourAccountIsRestricted("Test"), responseTokens?.Detail);
    }

    [Fact]
    public async Task TestGetBannedIpUserAuthTokens()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var user = await CreateTestUser(new User
        {
            Username = "user",
            Email = "user@mail.com",
            Passhash = password.GetPassHash(),
            Country = _mocker.User.GetRandomCountryCode()
        });

        // Act
        var response = await client.UseUserIp(BannedIp).PostAsJsonAsync("auth/token",
            new TokenRequest
            {
                Username = user.Username,
                Password = password
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();

        Assert.Contains(ApiErrorResponse.Detail.YouHaveBeenBanned, responseTokens?.Detail);
    }
}