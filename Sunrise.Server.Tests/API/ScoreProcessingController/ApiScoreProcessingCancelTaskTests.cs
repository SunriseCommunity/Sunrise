using System.Net;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreProcessingController;

[Collection("Integration tests collection")]
public class ApiScoreProcessingCancelTaskTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestCancelTaskWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsync("score-processing/1/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestCancelTaskWithNonSuperUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync("score-processing/1/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestCancelTaskWithMissingTask()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync("score-processing/999999/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestCancelPendingTaskSucceedsAndRecordsEvent()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var score = await CreateTestScore();
        var task = new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Delete,
            ScoreId = score.Id,
            Status = ScoreProcessingStatus.Pending,
            Priority = (int)ScoreProcessingPriority.Normal,
            CreatedAt = DateTime.UtcNow
        };
        await Database.ScoreProcessingTasks.AddQueueEntry(task);

        // Act
        var response = await client.PostAsync($"score-processing/{task.Id}/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedTask = await Database.ScoreProcessingTasks.GetTaskById(task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(ScoreProcessingStatus.Failed, updatedTask.Status);
        Assert.Equal(ScoreProcessingErrorCode.CancelledByOperator, updatedTask.ErrorCode);

        var (events, _) = await Database.Events.ScoreProcessing.GetEvents(types: [ScoreProcessingEventType.Cancelled]);
        Assert.Single(events);
        Assert.Equal(task.Id, events.Single().TaskId);
    }

    [Fact]
    public async Task TestCancelProcessingTaskReturnsConflict()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var score = await CreateTestScore();
        var task = new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Delete,
            ScoreId = score.Id,
            Status = ScoreProcessingStatus.Processing,
            Priority = (int)ScoreProcessingPriority.Normal,
            CreatedAt = DateTime.UtcNow
        };
        await Database.ScoreProcessingTasks.AddQueueEntry(task);

        // Act
        var response = await client.PostAsync($"score-processing/{task.Id}/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var updatedTask = await Database.ScoreProcessingTasks.GetTaskById(task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(ScoreProcessingStatus.Processing, updatedTask.Status);
    }
}