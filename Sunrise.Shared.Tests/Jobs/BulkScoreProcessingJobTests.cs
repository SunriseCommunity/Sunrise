using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Jobs;
using Sunrise.Tests.Abstracts;

namespace Sunrise.Shared.Tests.Jobs;

[Collection("Integration tests collection")]
public class BulkScoreProcessingJobTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task TestEnqueueByFilterShouldAddDbEntriesForMatchedScores()
    {
        // Arrange
        var user = await CreateTestUser();
        var firstScore = await CreateTestScore(user);
        var secondScore = await CreateTestScore(user);

        var scopeFactory = App.Server.Services.GetRequiredService<IServiceScopeFactory>();
        var job = new BulkScoreProcessingJob(scopeFactory);

        // Act
        await job.EnqueueByFilter(
            executorId: user.Id,
            action: ScoreTaskType.Delete,
            userId: user.Id,
            mode: null,
            mods: null,
            submissionStatus: null,
            beatmapStatus: null,
            submittedFrom: null,
            submittedTo: null,
            ct: CancellationToken.None);

        // Assert
        var scoreIds = new[]
        {
            firstScore.Id,
            secondScore.Id
        };

        var tasks = await Database.DbContext.ScoreProcessingTasks
            .AsNoTracking()
            .Where(task => task.TaskType == ScoreTaskType.Delete
                           && task.ScoreId.HasValue
                           && scoreIds.Contains(task.ScoreId.Value))
            .ToListAsync();

        Assert.Equal(2, tasks.Count);
        Assert.All(tasks,
            task =>
            {
                Assert.Equal(ScoreTaskType.Delete, task.TaskType);
                Assert.Equal((int)ScoreProcessingPriority.Low, task.Priority);
                Assert.Equal(ScoreProcessingStatus.Pending, task.Status);
            });
    }

    [Fact]
    public async Task TestEnqueueByFilterShouldNotAddDbEntriesWhenNoScoresMatchFilter()
    {
        // Arrange
        var userWithNoScores = await CreateTestUser();

        var anotherUser = await CreateTestUser();
        _ = await CreateTestScore(anotherUser);

        var scopeFactory = App.Server.Services.GetRequiredService<IServiceScopeFactory>();
        var job = new BulkScoreProcessingJob(scopeFactory);

        // Act
        await job.EnqueueByFilter(
            executorId: userWithNoScores.Id,
            action: ScoreTaskType.Recalculation,
            userId: userWithNoScores.Id,
            mode: null,
            mods: null,
            submissionStatus: null,
            beatmapStatus: null,
            submittedFrom: null,
            submittedTo: null,
            ct: CancellationToken.None);

        // Assert
        var createdTasks = await Database.DbContext.ScoreProcessingTasks
            .AsNoTracking()
            .CountAsync(task => task.TaskType == ScoreTaskType.Recalculation);

        Assert.Equal(0, createdTasks);
    }
}
