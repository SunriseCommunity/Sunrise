using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;

namespace Sunrise.Server.Tests.API.AuthController;

public class ApiAuthRegisterTests : ApiTest
{
    private readonly MockService _mocker = new();

    private string BannedIp => Configuration.BannedIps.FirstOrDefault() ?? throw new Exception("Banned IP not found");

    [Fact]
    public async Task TestRegisterUser()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var username = _mocker.User.GetRandomUsername();
        var email = _mocker.User.GetRandomEmail();

        // Act
        var response = await client.PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            });

        // Assert
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var responseTokens = JsonSerializer.Deserialize<TokenResponse>(responseString);

        Assert.NotNull(responseTokens);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(username: username);

        Assert.NotNull(user);
    }

    [Fact]
    public async Task TestRegisterUserCreatesRegisterEvent()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var username = _mocker.User.GetRandomUsername();
        var email = _mocker.User.GetRandomEmail();

        var ip = _mocker.User.GetRandomIp();

        // Act
        var response = await client.UseUserIp(ip).PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            });

        // Assert
        response.EnsureSuccessStatusCode();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var isEventRegistered = await database.EventService.UserEvent.IsIpCreatedAccountBefore(ip);

        Assert.True(isEventRegistered);
    }

    [Fact]
    public async Task TestRegisterUserGreeceFlag()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var username = _mocker.User.GetRandomUsername();
        var email = _mocker.User.GetRandomEmail();

        const string greeceIp = "102.38.248.255";

        // Act
        var response = await client.UseUserIp(greeceIp)
            .PostAsJsonAsync("auth/register",
                new RegisterRequest
                {
                    Username = username,
                    Password = password,
                    Email = email
                });

        // Assert
        response.EnsureSuccessStatusCode();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(username: username);

        Assert.NotNull(user);
        Assert.Equal((short)CountryCode.GR, user.Country);
    }

    [Fact]
    public async Task TestRegisterUserInvalidLengthUsername()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var username = _mocker.User.GetRandomUsername(64);
        var email = _mocker.User.GetRandomEmail();

        // Act
        var response = await client.PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("Username length", error?.Error);
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
    public async Task TestRegisterUserInvalidUsername(string username)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var email = _mocker.User.GetRandomEmail();

        // Act
        var response = await client.PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var responseError = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        var (_, expectedError) = username.IsValidUsername();

        Assert.Equal(expectedError, responseError?.Error);
    }

    [Fact]
    public async Task TestRegisterUserUsedUsername()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var password = _mocker.User.GetRandomPassword();
        var username = user.Username;
        var email = _mocker.User.GetRandomEmail();

        // Act
        var response = await client.PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("username already exists", error?.Error);
    }

    [Fact]
    public async Task TestRegisterUserUsedEmail()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var password = _mocker.User.GetRandomPassword();
        var username = _mocker.User.GetRandomUsername();
        var email = user.Email;

        // Act
        var response = await client.PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("email already exists", error?.Error);
    }

    [Fact]
    public async Task TestRegisterUserInvalidEmail()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var username = _mocker.User.GetRandomUsername();
        const string email = "invalid";

        // Act
        var response = await client.PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("Invalid email", error?.Error);
    }

    [Fact]
    public async Task TestRegisterUserBannedIp()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var password = _mocker.User.GetRandomPassword();
        var username = _mocker.User.GetRandomUsername();
        var email = _mocker.User.GetRandomEmail();

        // Act
        var response = await client.UseUserIp(BannedIp).PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("Your IP address is banned", error?.Error);
    }

    [Fact]
    public async Task TestRegisterUserWarnMultiaccount()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var ip = _mocker.User.GetRandomIp();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.EventService.UserEvent.CreateNewUserRegisterEvent(user.Id, ip, user);

        var password = _mocker.User.GetRandomPassword();
        var username = _mocker.User.GetRandomUsername();
        var email = _mocker.User.GetRandomEmail();

        // Act
        var response = await client.UseUserIp(ip).PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password,
                Email = email
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("Please don't create multiple accounts", error?.Error);
    }
}