using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserSearchTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestSearchUserEmptyQuery()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("user/search");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("query parameter", responseData?.Error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestSearchUserInvalidLimit(string limit)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var usernameQuery = _mocker.User.GetRandomUsername();

        // Act
        var response = await client.GetAsync($"user/search?query={usernameQuery}&limit={limit}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestSearchUserInvalidPage(string page)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var usernameQuery = _mocker.User.GetRandomUsername();

        // Act
        var response = await client.GetAsync($"user/search?query={usernameQuery}&page={page}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Fact]
    public async Task TestSearchUser()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var userData = new UserResponse(user);

        // Act
        var response = await client.GetAsync($"user/search?query={user.Username}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsync<UserResponse[]>();
        var responseUser = responseUsers?.FirstOrDefault();
        Assert.NotNull(responseUser);
        responseUser.UserStatus = null; // Ignore user status for comparison

        Assert.Equivalent(userData, responseUser);
    }

    [Fact]
    public async Task TestSearchUserPageAttribute()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        for (var i = 0; i < 51; i++)
        {
            var user = _mocker.User.GetRandomUser();
            user.Username = $"username_{i.ToString()}";

            await CreateTestUser(user);
        }

        // Act
        var response = await client.GetAsync("user/search?query=username&page=1");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsync<UserResponse[]>();
        Assert.NotNull(responseUsers);

        Assert.Single(responseUsers);
    }

    [Fact]
    public async Task TestSearchUserLimitAttribute()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        for (var i = 0; i < 2; i++)
        {
            var user = _mocker.User.GetRandomUser();
            user.Username = $"username_{i.ToString()}";

            await CreateTestUser(user);
        }

        // Act
        var response = await client.GetAsync("user/search?query=username&limit=1");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsync<UserResponse[]>();
        Assert.NotNull(responseUsers);

        Assert.Single(responseUsers);
    }

    [Fact]
    public async Task TestSearchUserIgnoreRestrictedUsers()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        // Act
        var response = await client.GetAsync($"user/search?query={user.Username}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseUsers = await response.Content.ReadFromJsonAsync<UserResponse[]>();
        Assert.NotNull(responseUsers);

        Assert.Empty(responseUsers);
    }
}