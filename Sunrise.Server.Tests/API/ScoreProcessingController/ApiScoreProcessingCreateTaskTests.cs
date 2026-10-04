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
public class ApiScoreProcessingCreateTaskTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestCreateTaskWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("score-processing",
            new CreateScoreProcessingTaskRequest
            {
                ScoreId = 1,
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestCreateTaskWithNonSuperUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("score-processing",
            new CreateScoreProcessingTaskRequest
            {
                ScoreId = 1,
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestCreateTaskWithMissingScore()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("score-processing",
            new CreateScoreProcessingTaskRequest
            {
                ScoreId = 999999,
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.ScoreNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestCreateTaskWithInvalidAction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var score = await CreateTestScore();

        // Act
        var response = await client.PostAsJsonAsync("score-processing",
            new CreateScoreProcessingTaskRequest
            {
                ScoreId = score.Id,
                Action = ScoreTaskType.Submission
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.UnableToQueueScoreProcessing, responseError?.Title);
        Assert.Contains(ApiErrorResponse.Detail.InvalidScoreProcessingAction, responseError?.Detail);
    }

    [Fact]
    public async Task TestCreateTaskConflictWhenActiveTaskExists()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var score = await CreateTestScore();

        // Act
        var firstResponse = await client.PostAsJsonAsync("score-processing",
            new CreateScoreProcessingTaskRequest
            {
                ScoreId = score.Id,
                Action = ScoreTaskType.Delete
            });

        var secondResponse = await client.PostAsJsonAsync("score-processing",
            new CreateScoreProcessingTaskRequest
            {
                ScoreId = score.Id,
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var responseError = await secondResponse.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Title.UnableToQueueScoreProcessing, responseError?.Title);
        Assert.Contains(ApiErrorResponse.Detail.ScoreAlreadyQueued, responseError?.Detail);
    }

    [Fact]
    public async Task TestCreateTaskSuccessQueuesTaskAndRecordsEvent()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var score = await CreateTestScore();

        // Act
        var response = await client.PostAsJsonAsync("score-processing",
            new CreateScoreProcessingTaskRequest
            {
                ScoreId = score.Id,
                Action = ScoreTaskType.Recalculation
            });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var task = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoreProcessingTaskResponse>();
        Assert.NotNull(task);
        Assert.Equal(ScoreTaskType.Recalculation, task.TaskType);
        Assert.Equal(ScoreProcessingStatus.Pending, task.Status);
        Assert.Equal(score.Id, task.ScoreId);

        var (tasks, totalTasks) = await Database.ScoreProcessingTasks.GetTasks(scoreId: score.Id);
        Assert.Equal(1, totalTasks);
        Assert.Equal(ScoreTaskType.Recalculation, tasks.Single().TaskType);

        var (events, totalEvents) = await Database.Events.ScoreProcessing.GetEvents();
        Assert.Equal(1, totalEvents);
        var recordedEvent = events.Single();
        Assert.Equal(ScoreProcessingEventType.RecalculationRequested, recordedEvent.EventType);
        Assert.Equal(score.Id, recordedEvent.ScoreId);
        Assert.Equal(superUser.Id, recordedEvent.ExecutorId);
    }
}
