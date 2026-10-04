using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreProcessingController;

[Collection("Integration tests collection")]
public class ApiScoreProcessingGetTasksTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetTasksWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("score-processing");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetTasksWithNonSuperUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("score-processing");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestGetTasksReturnsTasksWithScorePreview()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var score = await CreateTestScore();

        await Database.ScoreProcessingTasks.AddQueueEntry(new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Delete,
            ScoreId = score.Id,
            Priority = (int)ScoreProcessingPriority.Normal,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await client.GetAsync("score-processing");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoreProcessingTasksResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);

        var task = result.Tasks.Single();
        Assert.Equal(score.Id, task.ScoreId);
        Assert.NotNull(task.Score);
        Assert.Equal(score.Id, task.Score.Score.Id);
    }

    [Fact]
    public async Task TestGetTasksFiltersByTaskType()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var deleteScore = await CreateTestScore();
        await Database.ScoreProcessingTasks.AddQueueEntry(new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Delete,
            ScoreId = deleteScore.Id,
            Priority = (int)ScoreProcessingPriority.Normal,
            CreatedAt = DateTime.UtcNow
        });

        var recalculationScore = await CreateTestScore();
        await Database.ScoreProcessingTasks.AddQueueEntry(new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Recalculation,
            ScoreId = recalculationScore.Id,
            Priority = (int)ScoreProcessingPriority.Normal,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await client.GetAsync("score-processing?task_type=Delete");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoreProcessingTasksResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(ScoreTaskType.Delete, result.Tasks.Single().TaskType);
    }

    [Fact]
    public async Task TestGetTasksRespectsPagination()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        for (var i = 0; i < 3; i++)
        {
            var score = await CreateTestScore();
            await Database.ScoreProcessingTasks.AddQueueEntry(new ScoreProcessingTask
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = score.Id,
                Priority = (int)ScoreProcessingPriority.Normal,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Act
        var response = await client.GetAsync("score-processing?limit=2&page=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoreProcessingTasksResponse>();
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Tasks.Count);
    }
}
