using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiUserSearchUserSensitivesListTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestSearchUserSensitivesListWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("user/search/list");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestSearchUserSensitivesListWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/search/list");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestSearchUserSensitivesListWithAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/search/list");

        // Assert
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success status code but got {response.StatusCode}. Response body: {responseBody}");

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Users);
        Assert.True(responseData.TotalCount >= 2, $"Expected at least 2 users but got {responseData.TotalCount}");
        Assert.True(responseData.Users.Count >= 2, $"Expected at least 2 users in list but got {responseData.Users.Count}");
    }

    [Fact]
    public async Task TestSearchUserSensitivesListWithQuery()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var testUser = _mocker.User.GetRandomUser();
        testUser.Username = "TestUser123";
        await CreateTestUser(testUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/search/list?query={testUser.Username}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();
        Assert.NotNull(responseData);
        Assert.True(responseData.Users.Count > 0);

        var foundUser = responseData.Users.FirstOrDefault(u => u.Username == testUser.Username);
        Assert.NotNull(foundUser);
        Assert.Equal(testUser.Id, foundUser.Id);
        Assert.Equal(testUser.Email, foundUser.Email);
    }

    [Fact]
    public async Task TestSearchUserSensitivesListByEmail()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var testUser = _mocker.User.GetRandomUser();
        testUser.Email = "unique@test.com";
        await CreateTestUser(testUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/search/list?query=unique@test.com");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Users);
        Assert.True(responseData.Users.Count > 0, "Expected at least one user in response");

        var foundUser = responseData.Users.FirstOrDefault(u => u.Email == testUser.Email);
        Assert.NotNull(foundUser);
        Assert.Equal(testUser.Id, foundUser.Id);
    }

    [Fact]
    public async Task TestSearchUserSensitivesListIncludesRestrictedUsers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var restrictedUser = _mocker.User.GetRandomUser();
        restrictedUser.Username = "RestrictedUser123";
        await CreateTestUser(restrictedUser);
        await Database.Users.Moderation.RestrictPlayer(restrictedUser.Id, null, "Test");

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/search/list?query={restrictedUser.Username}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();
        Assert.NotNull(responseData);

        var foundUser = responseData.Users.FirstOrDefault(u => u.Username == restrictedUser.Username);
        Assert.NotNull(foundUser);
        Assert.True(foundUser.IsRestricted);
    }

    [Fact]
    public async Task TestSearchUserSensitivesListWithNullQuery()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/search/list");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();
        Assert.NotNull(responseData);
        Assert.True(responseData.TotalCount >= 2);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestSearchUserSensitivesListInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/search/list?limit={limit}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("test")]
    public async Task TestSearchUserSensitivesListInvalidPage(string page)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/search/list?page={page}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestSearchUserSensitivesListPagination()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        for (var i = 0; i < 3; i++)
        {
            var user = _mocker.User.GetRandomUser();
            user.Username = $"PaginationUser_{i:D3}";
            await CreateTestUser(user);
        }

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act 
        var responsePage1 = await client.GetAsync("user/search/list?query=PaginationUser&limit=2&page=1");
        var responsePage2 = await client.GetAsync("user/search/list?query=PaginationUser&limit=2&page=2");

        // Assert
        responsePage1.EnsureSuccessStatusCode();
        responsePage2.EnsureSuccessStatusCode();

        var dataPage1 = await responsePage1.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();
        var dataPage2 = await responsePage2.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();

        Assert.NotNull(dataPage1);
        Assert.NotNull(dataPage2);

        Assert.Equal(2, dataPage1.Users.Count);
        Assert.Single(dataPage2.Users);
        Assert.Equal(3, dataPage1.TotalCount);
    }

    [Fact]
    public async Task TestSearchUserSensitivesListLimitAttribute()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        for (var i = 0; i < 3; i++)
        {
            var user = _mocker.User.GetRandomUser();
            user.Username = $"LimitTestUser_{i}";
            await CreateTestUser(user);
        }

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/search/list?query=LimitTestUser&limit=2");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();
        Assert.NotNull(responseData);
        Assert.NotNull(responseData.Users);

        Assert.Equal(2, responseData.Users.Count);
        Assert.Equal(3, responseData.TotalCount);
    }

    [Fact]
    public async Task TestSearchUserSensitivesListResponseContainsSensitiveData()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var testUser = _mocker.User.GetRandomUser();
        testUser.Username = "SensitiveDataUser";
        testUser.Email = "sensitive@test.com";
        testUser.Description = "Test description";
        await CreateTestUser(testUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/search/list?query={testUser.Username}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();
        Assert.NotNull(responseData);

        var foundUser = responseData.Users.FirstOrDefault(u => u.Username == testUser.Username);
        Assert.NotNull(foundUser);

        // Verify sensitive data is present
        Assert.Equal(testUser.Email, foundUser.Email);
        Assert.Equal(testUser.Description, foundUser.Description);
        Assert.Equal(JsonStringFlagEnumHelper.SplitFlags(testUser.Privilege), foundUser.Privilege);
        Assert.Equal(testUser.Country, foundUser.Country);
        Assert.NotNull(foundUser.AvatarUrl);
        Assert.NotNull(foundUser.BannerUrl);
    }

    [Fact]
    public async Task TestSearchUserSensitivesListNoResults()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/search/list?query=NonExistentUser12345");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<UsersSensitiveListResponse>();
        Assert.NotNull(responseData);
        Assert.Empty(responseData.Users);
        Assert.Equal(0, responseData.TotalCount);
    }
}
