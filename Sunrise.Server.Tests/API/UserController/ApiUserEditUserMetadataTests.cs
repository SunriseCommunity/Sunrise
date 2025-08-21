using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserEditUserMetadataTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestEditUserMetadataUserWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsync("user/edit/metadata", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestEditUserMetadataInvalidBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var json = "{{\"string\":\"123\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("user/edit/metadata", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestEditMetadataWithInvalidLocationLength()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var newLocation = _mocker.GetRandomString(33);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/metadata",
            new EditUserMetadataRequest
            {
                Location = newLocation
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestEditUserMetadata()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var userMetadata = await Database.Users.Metadata.GetUserMetadata(user.Id);
        if (userMetadata is null)
            throw new Exception("User metadata not found");

        userMetadata = _mocker.User.SetRandomUserMetadata(userMetadata);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/metadata",
            new EditUserMetadataRequest
            {
                Location = userMetadata.Location,
                Discord = userMetadata.Discord,
                Interest = userMetadata.Interest,
                Occupation = userMetadata.Occupation,
                Playstyle = JsonStringFlagEnumHelper.SplitFlags(userMetadata.Playstyle),
                Telegram = userMetadata.Telegram,
                Twitch = userMetadata.Twitch,
                Twitter = userMetadata.Twitter,
                Website = userMetadata.Website
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var newUserMetadata = await Database.Users.Metadata.GetUserMetadata(user.Id);
        if (newUserMetadata is null)
            throw new Exception("User metadata not found");

        userMetadata.User = null!; // Ignore for comparison

        Assert.Equivalent(newUserMetadata, userMetadata);
    }

    [Fact]
    public async Task TestEditUserMetadataPartly()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var userMetadata = await Database.Users.Metadata.GetUserMetadata(user.Id);
        if (userMetadata is null)
            throw new Exception("User metadata not found");

        userMetadata.Playstyle = UserPlaystyle.Tablet & UserPlaystyle.Mouse;
        userMetadata.Occupation = "Testing 123";
        userMetadata.Interest = "Testing 123";

        await Database.Users.Metadata.UpdateUserMetadata(userMetadata);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/metadata",
            new EditUserMetadataRequest
            {
                Playstyle = JsonStringFlagEnumHelper.SplitFlags(UserPlaystyle.None),
                Occupation = ""
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var newUserMetadata = await Database.Users.Metadata.GetUserMetadata(user.Id);
        if (newUserMetadata is null)
            throw new Exception("User metadata not found");

        Assert.Equivalent(newUserMetadata.Playstyle, UserPlaystyle.None);
        Assert.Equivalent(newUserMetadata.Occupation, "");
        Assert.Equivalent(newUserMetadata.Interest, "Testing 123");
    }


    [Fact]
    public async Task TestEditUserMetadataWithActiveRestriction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var userMetadata = await Database.Users.Metadata.GetUserMetadata(user.Id);
        if (userMetadata is null)
            throw new Exception("User metadata not found");

        userMetadata = _mocker.User.SetRandomUserMetadata(userMetadata);

        var result = await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");
        if (result.IsFailure)
            throw new Exception(result.Error);

        // Act
        var response = await client.PostAsJsonAsync("user/edit/metadata",
            new EditUserMetadataRequest
            {
                Location = userMetadata.Location,
                Discord = userMetadata.Discord,
                Interest = userMetadata.Interest,
                Occupation = userMetadata.Occupation,
                Playstyle = JsonStringFlagEnumHelper.SplitFlags(userMetadata.Playstyle),
                Telegram = userMetadata.Telegram,
                Twitch = userMetadata.Twitch,
                Twitter = userMetadata.Twitter,
                Website = userMetadata.Website
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}