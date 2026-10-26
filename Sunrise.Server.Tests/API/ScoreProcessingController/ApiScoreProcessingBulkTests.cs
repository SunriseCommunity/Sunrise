using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreProcessingController;

[Collection("Integration tests collection")]
public class ApiScoreProcessingBulkTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestBulkByIdsWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("score-processing/bulk",
            new BulkScoreProcessingRequest
            {
                ScoreIds = [1],
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestBulkByIdsWithNonSuperUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("score-processing/bulk",
            new BulkScoreProcessingRequest
            {
                ScoreIds = [1],
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestBulkByIdsQueuesExistingScoresAndSkipsMissing()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var firstScore = await CreateTestScore();
        var secondScore = await CreateTestScore();

        // Act
        var response = await client.PostAsJsonAsync("score-processing/bulk",
            new BulkScoreProcessingRequest
            {
                ScoreIds = [firstScore.Id, secondScore.Id, 999999],
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<BulkScoreProcessingResultResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Queued);
        Assert.Equal(1, result.Skipped);

        var (_, totalEvents) = await Database.Events.ScoreProcessing.GetEvents();
        Assert.Equal(2, totalEvents);
    }

    [Fact]
    public async Task TestBulkByIdsQueuesExistingScoresAndCreatesScoreProcessingTask()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var firstScore = await CreateTestScore();
        var secondScore = await CreateTestScore();

        // Act
        var response = await client.PostAsJsonAsync("score-processing/bulk",
            new BulkScoreProcessingRequest
            {
                ScoreIds = [firstScore.Id, secondScore.Id],
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<BulkScoreProcessingResultResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Queued);

        var (_, totalTasks) = await Database.ScoreProcessingTasks.GetTasks();
        Assert.Equal(2, totalTasks);
    }

    [Fact]
    public async Task TestBulkByIdsRejectsIfScoreIdsCountViolatesMaxCount()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("score-processing/bulk",
            new BulkScoreProcessingRequest
            {
                ScoreIds = Enumerable.Range(1, 101).ToList(),
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.TooManyScoreIds, responseError?.Detail);
    }
}