using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.AuthController;

public class ApiAuthRefreshTests : ApiTest
{
    private static string BannedIp => Configuration.BannedIps.FirstOrDefault() ?? throw new Exception("Banned IP not found");

    [Fact]
    public async Task TestRefreshToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);

        // Act
        var response = await client.PostAsJsonAsync("auth/refresh",
            new RefreshTokenRequest
            {
                RefreshToken = tokens.RefreshToken
            });

        // Assert
        response.EnsureSuccessStatusCode();

        var responseTokens = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();

        Assert.NotNull(responseTokens);
        Assert.NotNull(responseTokens.Token);
        Assert.True(responseTokens.ExpiresIn > 0);
    }

    [Fact]
    public async Task TestMissingRefreshToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("auth/refresh",
            new
            {
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("One or more required fields are missing", error?.Error);
    }

    [Fact]
    public async Task TestInvalidRefreshToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var refreshToken = Guid.NewGuid().ToString("N");

        // Act
        var response = await client.PostAsJsonAsync("auth/refresh",
            new RefreshTokenRequest
            {
                RefreshToken = refreshToken
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("Invalid refresh_token", error?.Error);
    }

    [Fact]
    public async Task TestRestrictedUserInvalidRefreshToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        var tokens = await GetUserAuthTokens(user);

        // Act
        var response = await client.PostAsJsonAsync("auth/refresh",
            new RefreshTokenRequest
            {
                RefreshToken = tokens.RefreshToken
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("Invalid refresh_token", error?.Error);
    }

    [Fact]
    public async Task TestBannedIpUserRefreshToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);

        // Act
        var response = await client.UseUserIp(BannedIp).PostAsJsonAsync("auth/refresh",
            new RefreshTokenRequest
            {
                RefreshToken = tokens.RefreshToken
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("Your IP address is banned", error?.Error);
    }
}