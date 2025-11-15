using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using Sunrise.Tests;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiUserGetUserMetadataTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetUserMetadataNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var userId = _mocker.GetRandomInteger(minInt: 2);

        // Act
        var response = await client.GetAsync($"user/{userId}/metadata");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserMetadataNotFound, responseContent?.Detail);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserMetadataInvalidUserId(string userId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/metadata");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUserMetadata()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var userMetadata = await Database.Users.Metadata.GetUserMetadata(user.Id);
        if (userMetadata is null)
            throw new Exception("User metadata not found");

        userMetadata = _mocker.User.SetRandomUserMetadata(userMetadata);

        var arrangeUserMetadataResult = await Database.Users.Metadata.UpdateUserMetadata(userMetadata);

        if (arrangeUserMetadataResult.IsFailure)
            throw new Exception(arrangeUserMetadataResult.Error);

        var userMetadataData = new UserMetadataResponse(userMetadata);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/metadata");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseMetadata = await response.Content.ReadFromJsonAsyncWithAppConfig<UserMetadataResponse>();
        Assert.NotNull(responseMetadata);

        Assert.Equivalent(userMetadataData, responseMetadata);
    }


    [Fact]
    public async Task TestGetUserMetadataRestrictedNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/metadata");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserIsRestricted, responseContent?.Detail);
    }

    [Fact]
    public async Task TestAdminGetUserMetadataRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var userMetadata = await Database.Users.Metadata.GetUserMetadata(targetUser.Id);
        if (userMetadata is null)
            throw new Exception("User metadata not found");

        userMetadata = _mocker.User.SetRandomUserMetadata(userMetadata);
        await Database.Users.Metadata.UpdateUserMetadata(userMetadata);

        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Test");

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var userMetadataData = new UserMetadataResponse(userMetadata);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/metadata");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseMetadata = await response.Content.ReadFromJsonAsyncWithAppConfig<UserMetadataResponse>();
        Assert.NotNull(responseMetadata);

        Assert.Equivalent(userMetadataData, responseMetadata);
    }
}