using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminEditUserPrivilegeTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminEditUserPrivilegeWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/edit/privilege", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/edit/privilege", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync("user/999999/edit/privilege",
            new StringContent("{\"privilege\":[\"User\"]}", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeWithoutBody()
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
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.ValidationError, responseError?.Title);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilege()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        Assert.Equal(UserPrivilege.User, targetUser.Privilege);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Supporter
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserPrivilege.Supporter, updatedUser.Privilege);

        var (totalCount, events) = await Database.Events.Users.GetUserEvents(targetUser.Id,
            new QueryOptions
            {
                QueryModifier = q => q.Cast<EventUser>().Where(e => e.EventType == UserEventType.ChangePrivilege)
            });

        Assert.Equal(1, totalCount);
        var privilegeChangeEvent = events.First();
        Assert.Equal(targetUser.Id, privilegeChangeEvent.UserId);
        Assert.Equal(UserEventType.ChangePrivilege, privilegeChangeEvent.EventType);

        var data = privilegeChangeEvent.GetData<JsonElement>();

        Assert.Equal((int)UserPrivilege.User, data.GetProperty("OldPrivilege").GetInt32());
        Assert.Equal((int)UserPrivilege.Supporter, data.GetProperty("NewPrivilege").GetInt32());
        Assert.Equal(adminUser.Id, data.GetProperty("UpdatedById").GetInt32());
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeWithMultipleFlags()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var combinedPrivilege = UserPrivilege.Supporter | UserPrivilege.Bat;
        var expectedPrivilege = JsonStringFlagEnumHelper.CombineFlags(new[]
        {
            UserPrivilege.Supporter,
            UserPrivilege.Bat
        });

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Supporter,
                    UserPrivilege.Bat
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(expectedPrivilege, updatedUser.Privilege);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeWithSamePrivilege()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = _mocker.User.GetRandomUser();
        targetUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(targetUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act 
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Supporter
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InsufficientPrivileges, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeWithLowerPrivilege()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = _mocker.User.GetRandomUser();
        targetUser.Privilege = UserPrivilege.Developer;
        await CreateTestUser(targetUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act 
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Supporter
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InsufficientPrivileges, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeWithSameRankPrivilege()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin | UserPrivilege.Supporter;
        await CreateTestUser(adminUser);

        var targetUser = _mocker.User.GetRandomUser();
        targetUser.Privilege = UserPrivilege.User;
        await CreateTestUser(targetUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act 
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Admin
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InsufficientPrivileges, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeCanAddLowerPrivilegeForTheUserWithSamePrivilegeRank()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = _mocker.User.GetRandomUser();
        user.Privilege = UserPrivilege.Admin;
        await CreateTestUser(user);

        var targetUser = _mocker.User.GetRandomUser();
        targetUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(targetUser);

        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Supporter,
                    UserPrivilege.Admin
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserPrivilege.Supporter | UserPrivilege.Admin, updatedUser.Privilege);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeCanRemoveLowerPrivilegeForTheUserWithSamePrivilegeRank()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = _mocker.User.GetRandomUser();
        user.Privilege = UserPrivilege.Admin;
        await CreateTestUser(user);

        var targetUser = _mocker.User.GetRandomUser();
        targetUser.Privilege = UserPrivilege.Admin | UserPrivilege.Supporter;
        await CreateTestUser(targetUser);

        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Admin
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserPrivilege.Admin, updatedUser.Privilege);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeCantEditToTheSameHigherPrivilegeAsExecutor()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        Assert.Equal(UserPrivilege.User, targetUser.Privilege);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Admin
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InsufficientPrivileges, responseError?.Detail);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserPrivilege.User, updatedUser.Privilege);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeToNone()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = _mocker.User.GetRandomUser();
        targetUser.Privilege = UserPrivilege.Supporter;
        await CreateTestUser(targetUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.User
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserPrivilege.User, updatedUser.Privilege);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeForRestrictedUser()
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

        // Act
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Supporter
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserPrivilege.Supporter, updatedUser.Privilege);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeToDeveloperShouldFailWhileModifyingPrivilegeOfHigherThanExecutors()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act - Admin cannot set Developer privilege (Developer > Admin)
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = new[]
                {
                    UserPrivilege.Developer
                }
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InsufficientPrivileges, responseError?.Detail);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserPrivilege.User, updatedUser.Privilege);
    }

    [Fact]
    public async Task TestAdminEditUserPrivilegeWithEmptyArray()
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
        var response = await client.PostAsJsonAsync($"user/{targetUser.Id}/edit/privilege",
            new EditUserPrivilegeRequest
            {
                Privilege = Array.Empty<UserPrivilege>()
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await Database.Users.GetUser(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserPrivilege.User, updatedUser.Privilege);
    }
}
