using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminChangeUserCountryTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminChangeUserCountryWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/country/change",
            new CountryChangeRequest
            {
                NewCountry = _mocker.User.GetRandomCountryCode()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminChangeUserCountryWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/country/change",
            new CountryChangeRequest
            {
                NewCountry = _mocker.User.GetRandomCountryCode()
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminChangeUserCountryWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/999999/country/change",
            new CountryChangeRequest
            {
                NewCountry = _mocker.User.GetRandomCountryCode()
            });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminChangeUserCountryWithoutBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/country/change", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Theory]
    [InlineData("1245")]
    [InlineData("-1")]
    [InlineData("01")]
    [InlineData("peppyland")]
    [InlineData("愛")]
    public async Task TestAdminChangeUserCountryWithInvalidCountry(string newCountry)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var json = $"{{\"new_country\":\"{newCountry}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/country/change", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Equal(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestAdminChangeUserCountry()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var oldCountry = targetUser.Country;

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newCountry = _mocker.User.GetRandomCountryCode();

        while (newCountry == oldCountry)
        {
            newCountry = _mocker.User.GetRandomCountryCode();
        }

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/country/change",
            new CountryChangeRequest
            {
                NewCountry = newCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(updatedUser.Country, newCountry);
    }

    [Fact]
    public async Task TestAdminChangeUserCountrySkipsCooldown()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        // Change country once to trigger cooldown
        var updateCountryResult = await Database.Users.UpdateUserCountry(new UserEventAction(targetUser, "127.0.0.1", targetUser.Id), targetUser.Country, CountryCode.AL);
        if (updateCountryResult.IsFailure)
            throw new Exception(updateCountryResult.Error);

        var lastUserCountryChange = await Database.Events.Users.GetLastUserCountryChangeEvent(targetUser.Id);
        Assert.NotNull(lastUserCountryChange);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newCountry = CountryCode.AD;

        // Act - Admin should be able to change country even if cooldown is active
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/country/change",
            new CountryChangeRequest
            {
                NewCountry = newCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(updatedUser.Country, newCountry);
    }

    [Fact]
    public async Task TestAdminChangeUserCountryToUnknownCountry()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/country/change",
            new CountryChangeRequest
            {
                NewCountry = CountryCode.XX
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.CantChangeCountryToUnknown, errorResponse?.Detail);
    }

    [Fact]
    public async Task TestAdminChangeUserCountryToSameCountry()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = _mocker.User.GetRandomUser();
        var sameCountry = CountryCode.BR;
        targetUser.Country = sameCountry;
        await CreateTestUser(targetUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/country/change",
            new CountryChangeRequest
            {
                NewCountry = sameCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.CantChangeCountryToTheSameOne, errorResponse?.Detail);
    }

    [Fact]
    public async Task TestAdminChangeUserCountryForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Test");

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newCountry = _mocker.User.GetRandomCountryCode();

        while (newCountry == targetUser.Country)
        {
            newCountry = _mocker.User.GetRandomCountryCode();
        }

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/country/change",
            new CountryChangeRequest
            {
                NewCountry = newCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);

        Assert.Equal(updatedUser.Country, newCountry);
    }

    [Fact]
    public async Task TestAdminChangeUserCountryLogsEvent()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        var oldCountry = targetUser.Country;

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var newCountry = _mocker.User.GetRandomCountryCode();

        while (newCountry == oldCountry)
        {
            newCountry = _mocker.User.GetRandomCountryCode();
        }

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/country/change",
            new CountryChangeRequest
            {
                NewCountry = newCountry
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var lastEvent = await Database.Events.Users.GetLastUserCountryChangeEvent(targetUser.Id);
        var data = lastEvent?.GetData<UserCountryChanged>();

        Assert.NotNull(lastEvent);
        Assert.Equal(newCountry, data!.NewCountry);
        Assert.Equal(oldCountry, data.OldCountry);
        Assert.Equal(adminUser.Id, data.UpdatedById);
    }
}
