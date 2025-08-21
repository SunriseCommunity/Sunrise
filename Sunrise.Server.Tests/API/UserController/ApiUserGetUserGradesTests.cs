using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserGetUserGradesTests : ApiTest
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Select(mode => new object[]
        {
            mode
        });
    }

    [Fact]
    public async Task TestGetUserGradesNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var userId = _mocker.GetRandomInteger();

        // Act
        var response = await client.GetAsync($"user/{userId}/grades?mode=0");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserGradesNotFound, responseContent?.Detail);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserGradesInvalidRoute(string userId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/grades?mode=0");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestGetUserGrades(GameMode gameMode)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, gameMode);
        if (userGrades is null)
            throw new Exception("User grades not found");

        userGrades = _mocker.User.SetRandomUserGrades(userGrades);

        var arrangeUserGradesResult = await Database.Users.Grades.UpdateUserGrades(userGrades);

        if (arrangeUserGradesResult.IsFailure)
            throw new Exception(arrangeUserGradesResult.Error);

        var userGradesData = new GradesResponse(userGrades);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/grades?mode={(int)gameMode}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseGrades = await response.Content.ReadFromJsonAsyncWithAppConfig<GradesResponse>();
        Assert.NotNull(responseGrades);

        Assert.Equivalent(userGradesData, responseGrades);
    }


    [Fact]
    public async Task TestGetUserGradesRestrictedNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/grades?mode=0");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserIsRestricted, responseContent?.Detail);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserGradesWithInvalidModeQuery(string mode)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/grades?mode={mode}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}