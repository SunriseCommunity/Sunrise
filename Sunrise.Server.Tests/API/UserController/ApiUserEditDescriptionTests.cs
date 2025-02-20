using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserEditDescriptionTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestEditDescriptionWithoutAuthToken()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("user/edit/description",
            new EditDescriptionRequest
            {
                Description = _mocker.GetRandomString()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Fact]
    public async Task TestEditDescriptionWithActiveRestriction()
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
        var response = await client.PostAsJsonAsync("user/edit/description",
            new EditDescriptionRequest
            {
                Description = _mocker.GetRandomString()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Invalid session", responseError?.Error);
    }

    [Fact]
    public async Task TestEditDescriptionWithoutBody()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/description", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Description is required", responseError?.Error);
    }

    [Fact]
    public async Task TestEditDescriptionWithInvalidDescriptionLength()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var newDescription = _mocker.GetRandomString(2001);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/description",
            new EditDescriptionRequest
            {
                Description = newDescription
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Description is too long", responseError?.Error);
    }

    [Fact]
    public async Task TestEditDescription()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var newDescription = _mocker.GetRandomString();

        // Act
        var response = await client.PostAsJsonAsync("user/edit/description",
            new EditDescriptionRequest
            {
                Description = newDescription
            });

        // Assert
        response.EnsureSuccessStatusCode();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var updatedUser = await database.UserService.GetUser(user.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(newDescription, updatedUser.Description);
    }
}