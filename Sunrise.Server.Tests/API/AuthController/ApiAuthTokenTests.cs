using System.Net;
using System.Net.Http.Json;
using Sunrise.Server.API.Serializable.Request;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Services;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.API.AuthController;

public class ApiAuthTokenTests : ApiTest
{
    private readonly MockService _mocker = new();

    private static string BannedIp => Configuration.BannedIps.FirstOrDefault() ?? throw new Exception("Banned IP not found");

    [Fact]
    public async Task TestGetUserAuthTokens()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

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

        var responseTokens = await response.Content.ReadFromJsonAsync<TokenResponse>();

        Assert.NotNull(responseTokens);
        Assert.NotNull(responseTokens.Token);
        Assert.NotNull(responseTokens.RefreshToken);

        Assert.True(responseTokens.ExpiresIn > 0);
    }

    [Fact]
    public async Task TestGetUserAuthTokensMissingBody()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new
            {
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("One or more required fields are missing", responseTokens?.Error);
    }

    [Fact]
    public async Task TestGetInvalidUserAuthTokens()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new TokenRequest
            {
                Username = "invalid",
                Password = "invalid"
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid credentials", responseTokens?.Error);
    }

    [Fact]
    public async Task TestGetUserAuthTokensInvalidPassword()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new TokenRequest
            {
                Username = user.Username,
                Password = "invalid"
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid credentials", responseTokens?.Error);
    }

    [Fact]
    public async Task TestGetRestrictedUserInvalidAuthTokens()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var user = await CreateTestUser(new User
        {
            Username = "user",
            Email = "user@mail.com",
            Passhash = password.GetPassHash(),
            Country = _mocker.User.GetRandomCountryCode()
        });

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new TokenRequest
            {
                Username = user.Username,
                Password = password
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.Contains("Your account is restricted", responseTokens?.Error);
    }

    [Fact]
    public async Task TestGetBannedIpUserAuthTokens()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseTokens = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.Contains("Your IP address is banned", responseTokens?.Error);
    }
}