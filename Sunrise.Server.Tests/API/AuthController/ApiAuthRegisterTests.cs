using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.AuthController;

public class ApiAuthRegisterTests : ApiTest
{
    private readonly MockService _mocker = new();

    private string BannedIp => Configuration.BannedIps.FirstOrDefault() ?? throw new Exception("Banned IP not found");

    [Fact]
    public async Task TestRegisterUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var user = await Database.Users.GetUser(username: username);

        Assert.NotNull(user);
    }

    [Fact]
    public async Task TestRegisterUserSetDefaultItems()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var user = await Database.Users.GetUser(username: username);

        Assert.NotNull(user);
        
        var userInventoryItem = await Database.Users.Inventory.GetInventoryItem(user.Id, ItemType.Hype);

        if (userInventoryItem == null)
            throw new Exception($"Could not find {ItemType.Hype} item in user inventory upon registration");

        Assert.Equal(ItemType.Hype, userInventoryItem.ItemType);
        Assert.Equal(Configuration.UserHypesWeekly, userInventoryItem.Quantity);
    }

    [Fact]
    public async Task TestRegisterUserCreatesRegisterEvent()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var userPrevRegistered = await Database.Events.Users.IsIpHasAnyRegisteredAccounts(ip);

        Assert.True(userPrevRegistered != null);
    }

    [Fact]
    public async Task TestRegisterUserGreeceFlag()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var user = await Database.Users.GetUser(username: username);

        Assert.NotNull(user);
        Assert.Equal((short)CountryCode.GR, user.Country);
    }

    [Fact]
    public async Task TestRegisterUserInvalidLengthUsername()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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
        var client = App.CreateClient().UseClient("api");

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
        var client = App.CreateClient().UseClient("api");

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
    public async Task TestRegisterUserUsedUsernameWithDifferentCase()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var randomUser = _mocker.User.GetRandomUser();
        randomUser.Username = randomUser.Username.ToLower();
        var user = await CreateTestUser(randomUser);

        var password = _mocker.User.GetRandomPassword();
        var username = user.Username.ToUpper();
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
        var client = App.CreateClient().UseClient("api");

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
    public async Task TestRegisterUserUsedEmailWithDifferentCase()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var randomUser = _mocker.User.GetRandomUser();
        randomUser.Email = randomUser.Email.ToLower();
        var user = await CreateTestUser(randomUser);

        var password = _mocker.User.GetRandomPassword();
        var username = _mocker.User.GetRandomUsername();
        var email = user.Email.ToUpper();

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
        var client = App.CreateClient().UseClient("api");

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
        var client = App.CreateClient().UseClient("api");

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
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var ip = _mocker.User.GetRandomIp();

        await Database.Events.Users.AddUserRegisterEvent(user.Id, ip, user);

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