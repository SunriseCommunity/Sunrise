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
public class ApiAdminEditIgnoreLoginDataTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminEditIgnoreLoginDataWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/edit/ignore-login-data", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditIgnoreLoginDataWithRegularUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/ignore-login-data",
            new EditIgnoreLoginDataRequest { IsIgnored = true });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditIgnoreLoginDataWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("user/999999/edit/ignore-login-data",
            new EditIgnoreLoginDataRequest { IsIgnored = true });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminEditIgnoreLoginDataWithoutBody()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(superUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/ignore-login-data", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestAdminEditIgnoreLoginDataSetIgnoreLoginDataToTrue()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(superUser);

        var targetUser = await CreateTestUser();
        var ip = _mocker.User.GetRandomIp();
        await Database.Events.Users.AddUserRegisterEvent(new UserEventAction(targetUser, ip, targetUser.Id), targetUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/ignore-login-data",
            new EditIgnoreLoginDataRequest { IsIgnored = true });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var accountFromSameIp = await Database.Events.Users.IsIpHasAnyRegisteredAccounts(ip);
        Assert.Null(accountFromSameIp);
    }

    [Fact]
    public async Task TestAdminEditIgnoreLoginDataSetIgnoreLoginDataToFalse()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(superUser);

        var targetUser = await CreateTestUser();
        var ip = _mocker.User.GetRandomIp();
        await Database.Events.Users.AddUserRegisterEvent(new UserEventAction(targetUser, ip, targetUser.Id), targetUser);
        
        await Database.Events.Users.SetRegisterEventIgnoredFromIpCheck(targetUser.Id, true);

        var accountFromSameIpBeforeUnignore = await Database.Events.Users.IsIpHasAnyRegisteredAccounts(ip);
        Assert.Null(accountFromSameIpBeforeUnignore);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/ignore-login-data",
            new EditIgnoreLoginDataRequest { IsIgnored = false });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var accountFromSameIpAfterUnignore = await Database.Events.Users.IsIpHasAnyRegisteredAccounts(ip);
        Assert.NotNull(accountFromSameIpAfterUnignore);
        Assert.Equal(targetUser.Id, accountFromSameIpAfterUnignore.Id);
    }

    [Fact]
    public async Task TestAdminEditIgnoreLoginDataMakesIpAllowsNewRegistration()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(superUser);

        var existingUser = await CreateTestUser();
        var ip = _mocker.User.GetRandomIp();
        await Database.Events.Users.AddUserRegisterEvent(new UserEventAction(existingUser, ip, existingUser.Id), existingUser);
        
        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var ignoreResponse = await client.PostAsJsonAsync($"user/{existingUser.Id}/edit/ignore-login-data",
            new EditIgnoreLoginDataRequest { IsIgnored = true });
        Assert.Equal(HttpStatusCode.OK, ignoreResponse.StatusCode);
        
        var registerClient = App.CreateClient().UseClient("api");

        // Act
        var registerResponse = await registerClient.UseUserIp(ip).PostAsJsonAsync("auth/register",
            new RegisterRequest
            {
                Username = _mocker.User.GetRandomUsername(),
                Password = _mocker.User.GetRandomPassword(),
                Email = _mocker.User.GetRandomEmail()
            });

        // Assert 
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditIgnoreLoginDataPreservesRegisterEventData()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(superUser);

        var targetUser = await CreateTestUser();
        var ip = _mocker.User.GetRandomIp();
        await Database.Events.Users.AddUserRegisterEvent(new UserEventAction(targetUser, ip, targetUser.Id), targetUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/ignore-login-data",
            new EditIgnoreLoginDataRequest { IsIgnored = true });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, registerEvents) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new Shared.Database.Objects.QueryOptions
            {
                QueryModifier = q => q.Cast<Sunrise.Shared.Database.Models.Events.EventUser>()
                    .Where(e => e.EventType == UserEventType.Register)
            });

        Assert.NotEmpty(registerEvents);
        var registerEvent = registerEvents.First();

        var data = registerEvent.GetData<UserRegistered>();
        Assert.NotNull(data);
        Assert.True(data.IsExemptFromMultiaccountCheck);
        Assert.NotNull(data.RegisterData);
        Assert.Equal(targetUser.Username, data.RegisterData.Username);
        Assert.Equal(targetUser.Email, data.RegisterData.Email);
    }
}
