using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserEditDescriptionTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestEditDescriptionWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("user/edit/description",
            new EditDescriptionRequest
            {
                Description = _mocker.GetRandomString()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestEditDescriptionWithActiveRestriction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var result = await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");
        if (result.IsFailure)
            throw new Exception(result.Error);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/description",
            new EditDescriptionRequest
            {
                Description = _mocker.GetRandomString()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestEditDescriptionWithoutBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/description", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestEditDescriptionWithInvalidDescriptionLength()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestEditDescription()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

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

        var updatedUser = await Database.Users.GetUser(user.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(newDescription, updatedUser.Description);
    }
}