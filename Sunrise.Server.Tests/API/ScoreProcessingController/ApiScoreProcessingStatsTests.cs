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
public class ApiScoreProcessingStatsTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetStatsWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("score-processing/stats");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetStatsWithNonSuperUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("score-processing/stats");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestGetStatsReturnsCountsAndEta()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        foreach (var status in new[] { ScoreProcessingStatus.Pending, ScoreProcessingStatus.Pending, ScoreProcessingStatus.Processing, ScoreProcessingStatus.Failed })
        {
            var score = await CreateTestScore();
            await Database.ScoreProcessingTasks.AddQueueEntry(new ScoreProcessingTask
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = score.Id,
                Status = status,
                Priority = (int)ScoreProcessingPriority.Normal,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Act
        var response = await client.GetAsync("score-processing/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stats = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoreProcessingStatsResponse>();
        Assert.NotNull(stats);
        Assert.Equal(2, stats.Pending);
        Assert.Equal(1, stats.Processing);
        Assert.Equal(1, stats.Failed);
        Assert.NotNull(stats.EstimatedPendingCompletionSeconds);
        Assert.True(stats.EstimatedPendingCompletionSeconds > 0);
    }

    [Fact]
    public async Task TestGetStatsReturnsNullEtaWhenNoPending()
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
            Status = ScoreProcessingStatus.Failed,
            Priority = (int)ScoreProcessingPriority.Normal,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await client.GetAsync("score-processing/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stats = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoreProcessingStatsResponse>();
        Assert.NotNull(stats);
        Assert.Equal(0, stats.Pending);
        Assert.Equal(1, stats.Failed);
        Assert.Null(stats.EstimatedPendingCompletionSeconds);
    }
}
