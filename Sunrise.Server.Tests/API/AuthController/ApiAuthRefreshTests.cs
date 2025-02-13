using System.Net.Http.Json;
using System.Text.Json;
using Sunrise.Server.API.Serializable.Request;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Services;
using Sunrise.Server.Tests.Core;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Tests.API.AuthController;

public class ApiAuthRefreshTests : ApiTest
{
    private static string BannedIp => Configuration.BannedIps.FirstOrDefault() ?? throw new Exception("Banned IP not found");
    
    [Fact]
    public async Task TestRefreshToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("auth/refresh", new  { });

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);
        
        Assert.Contains("One or more required fields are missing", error?.Error);
    }
    
    [Fact]
    public async Task TestInvalidRefreshToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var refreshToken = Guid.NewGuid().ToString("N");

        // Act
        var response = await client.PostAsJsonAsync("auth/refresh",
            new RefreshTokenRequest
            {
                RefreshToken = refreshToken
            });

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);
        
        Assert.Contains("Invalid refresh_token", error?.Error);
    }
    
    [Fact]
    public async Task TestRestrictedUserInvalidRefreshToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");
        
        var tokens = await GetUserAuthTokens(user);
        
        // Act
        var response = await client.PostAsJsonAsync("auth/refresh",
            new RefreshTokenRequest
            {
                RefreshToken = tokens.RefreshToken
            });

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        
        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);
        
        Assert.Contains("Invalid refresh_token", error?.Error);
    }

    [Fact]
    public async Task TestBannedIpUserRefreshToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);

        // Act
        var response = await client.UseUserIp(BannedIp).PostAsJsonAsync("auth/refresh",
            new RefreshTokenRequest
            {
               RefreshToken = tokens.RefreshToken
            });

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(responseString);

        Assert.Contains("Your IP address is banned", error?.Error);
    }
}