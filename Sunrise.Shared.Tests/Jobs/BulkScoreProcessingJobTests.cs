using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Jobs;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Mods = osu.Shared.Mods;
using ScoreGameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using ScoreSubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Shared.Tests.Jobs;

[Collection("Integration tests collection")]
public class BulkScoreProcessingJobTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestEnqueueByFilterShouldAddDbEntriesForMatchedScores()
    {
        // Arrange
        var user = await CreateTestUser();
        var firstScore = await CreateTestScore(user);
        var secondScore = await CreateTestScore(user);
        var ignoredScore = await CreateTestScore(user);

        var scopeFactory = App.Server.Services.GetRequiredService<IServiceScopeFactory>();
        var job = new BulkScoreProcessingJob(scopeFactory);

        // Act
        await job.EnqueueByFilter(
            user.Id,
            ScoreTaskType.Delete,
            user.Id,
            null,
            null,
            null,
            null,
            null,
            null,
            CancellationToken.None);

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

        Assert.DoesNotContain(tasks, task => task.ScoreId == ignoredScore.Id);
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
            userWithNoScores.Id,
            ScoreTaskType.Recalculation,
            userWithNoScores.Id,
            null,
            null,
            null,
            null,
            null,
            null,
            CancellationToken.None);

        // Assert
        var createdTasks = await Database.DbContext.ScoreProcessingTasks
            .AsNoTracking()
            .CountAsync(task => task.TaskType == ScoreTaskType.Recalculation);

        Assert.Equal(0, createdTasks);
    }

    [Fact]
    public async Task TestEnqueueByFilterShouldFilterByMode()
    {
        // Arrange
        var user = await CreateTestUser();
        var matchingScore = await CreateConfiguredScore(user,
            score => score.GameMode = ScoreGameMode.Standard);
        _ = await CreateConfiguredScore(user,
            score => score.GameMode = ScoreGameMode.Mania);

        // Act
        await EnqueueByFilter(
            user.Id,
            ScoreTaskType.Delete,
            ScoreGameMode.Standard,
            null,
            null,
            null,
            null,
            null);

        // Assert
        await AssertQueuedScoreIds(ScoreTaskType.Delete, [matchingScore.Id]);
    }

    [Fact]
    public async Task TestEnqueueByFilterShouldFilterByMods()
    {
        // Arrange
        var user = await CreateTestUser();
        var matchingScore = await CreateConfiguredScore(user,
            score => score.Mods = Mods.Hidden);
        _ = await CreateConfiguredScore(user,
            score => score.Mods = Mods.HardRock);

        // Act
        await EnqueueByFilter(
            user.Id,
            ScoreTaskType.Recalculation,
            null,
            Mods.Hidden,
            null,
            null,
            null,
            null);

        // Assert
        await AssertQueuedScoreIds(ScoreTaskType.Recalculation, [matchingScore.Id]);
    }

    [Fact]
    public async Task TestEnqueueByFilterShouldFilterBySubmissionStatus()
    {
        // Arrange
        var user = await CreateTestUser();
        var matchingScore = await CreateConfiguredScore(user,
            score => score.SubmissionStatus = ScoreSubmissionStatus.Best);
        _ = await CreateConfiguredScore(user,
            score => score.SubmissionStatus = ScoreSubmissionStatus.Failed);

        // Act
        await EnqueueByFilter(
            user.Id,
            ScoreTaskType.Restore,
            null,
            null,
            ScoreSubmissionStatus.Best,
            null,
            null,
            null);

        // Assert
        await AssertQueuedScoreIds(ScoreTaskType.Restore, [matchingScore.Id]);
    }

    [Fact]
    public async Task TestEnqueueByFilterShouldFilterByBeatmapStatus()
    {
        // Arrange
        var user = await CreateTestUser();
        var matchingScore = await CreateConfiguredScore(user,
            score => score.BeatmapStatus = BeatmapStatus.Ranked);
        _ = await CreateConfiguredScore(user,
            score => score.BeatmapStatus = BeatmapStatus.Loved);

        // Act
        await EnqueueByFilter(
            user.Id,
            ScoreTaskType.Delete,
            null,
            null,
            null,
            BeatmapStatus.Ranked,
            null,
            null);

        // Assert
        await AssertQueuedScoreIds(ScoreTaskType.Delete, [matchingScore.Id]);
    }

    [Fact]
    public async Task TestEnqueueByFilterShouldFilterBySubmittedFrom()
    {
        // Arrange
        var user = await CreateTestUser();
        var from = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        var matchingScore = await CreateConfiguredScore(user,
            score => score.WhenPlayed = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc));
        _ = await CreateConfiguredScore(user,
            score => score.WhenPlayed = new DateTime(2025, 1, 9, 12, 0, 0, DateTimeKind.Utc));

        // Act
        await EnqueueByFilter(
            user.Id,
            ScoreTaskType.Recalculation,
            null,
            null,
            null,
            null,
            from,
            null);

        // Assert
        await AssertQueuedScoreIds(ScoreTaskType.Recalculation, [matchingScore.Id]);
    }

    [Fact]
    public async Task TestEnqueueByFilterShouldFilterBySubmittedTo()
    {
        // Arrange
        var user = await CreateTestUser();
        var to = new DateTime(2025, 1, 10, 23, 59, 59, DateTimeKind.Utc);

        var matchingScore = await CreateConfiguredScore(user,
            score => score.WhenPlayed = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc));
        _ = await CreateConfiguredScore(user,
            score => score.WhenPlayed = new DateTime(2025, 1, 11, 12, 0, 0, DateTimeKind.Utc));

        // Act
        await EnqueueByFilter(
            user.Id,
            ScoreTaskType.Restore,
            null,
            null,
            null,
            null,
            null,
            to);

        // Assert
        await AssertQueuedScoreIds(ScoreTaskType.Restore, [matchingScore.Id]);
    }

    [Fact]
    public async Task TestEnqueueByFilterShouldFilterBySubmittedFromAndSubmittedToWithSingleIntersection()
    {
        // Arrange
        var user = await CreateTestUser();
        var from = new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 1, 15, 23, 59, 59, DateTimeKind.Utc);

        var matchesBoth = await CreateConfiguredScore(user,
            score => score.WhenPlayed = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc));
        _ = await CreateConfiguredScore(user,
            score => score.WhenPlayed = new DateTime(2025, 1, 6, 12, 0, 0, DateTimeKind.Utc));
        _ = await CreateConfiguredScore(user,
            score => score.WhenPlayed = new DateTime(2025, 1, 20, 12, 0, 0, DateTimeKind.Utc));

        // Act
        await EnqueueByFilter(
            user.Id,
            ScoreTaskType.Delete,
            null,
            null,
            null,
            null,
            from,
            to);

        // Assert
        await AssertQueuedScoreIds(ScoreTaskType.Delete, [matchesBoth.Id]);
    }

    [Fact]
    public async Task TestEnqueueByFilterShouldFilterByModeAndBeatmapStatusWithSingleIntersection()
    {
        // Arrange
        var user = await CreateTestUser();

        var matchesBoth = await CreateConfiguredScore(user,
            score =>
            {
                score.GameMode = ScoreGameMode.Standard;
                score.BeatmapStatus = BeatmapStatus.Ranked;
            });

        _ = await CreateConfiguredScore(user,
            score =>
            {
                score.GameMode = ScoreGameMode.Standard;
                score.BeatmapStatus = BeatmapStatus.Loved;
            });

        _ = await CreateConfiguredScore(user,
            score =>
            {
                score.GameMode = ScoreGameMode.Mania;
                score.BeatmapStatus = BeatmapStatus.Ranked;
            });

        // Act
        await EnqueueByFilter(
            user.Id,
            ScoreTaskType.Recalculation,
            ScoreGameMode.Standard,
            null,
            null,
            BeatmapStatus.Ranked,
            null,
            null);

        // Assert
        await AssertQueuedScoreIds(ScoreTaskType.Recalculation, [matchesBoth.Id]);
    }

    private async Task<Score> CreateConfiguredScore(User user, Action<Score> configure)
    {
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        configure(score);

        await CreateTestScore(score);

        return score;
    }

    private async Task EnqueueByFilter(
        int userId,
        ScoreTaskType action,
        ScoreGameMode? mode,
        Mods? mods,
        ScoreSubmissionStatus? submissionStatus,
        BeatmapStatus? beatmapStatus,
        DateTime? submittedFrom,
        DateTime? submittedTo)
    {
        var scopeFactory = App.Server.Services.GetRequiredService<IServiceScopeFactory>();
        var job = new BulkScoreProcessingJob(scopeFactory);

        await job.EnqueueByFilter(
            userId,
            action,
            userId,
            mode,
            mods,
            submissionStatus,
            beatmapStatus,
            submittedFrom,
            submittedTo,
            CancellationToken.None);
    }

    private async Task AssertQueuedScoreIds(ScoreTaskType action, int[] expectedScoreIds)
    {
        var queuedScoreIds = await Database.DbContext.ScoreProcessingTasks
            .AsNoTracking()
            .Where(task => task.TaskType == action && task.ScoreId.HasValue)
            .Select(task => task.ScoreId!.Value)
            .OrderBy(id => id)
            .ToListAsync();

        Assert.Equal(expectedScoreIds.Length, queuedScoreIds.Count);
        Assert.Equal(expectedScoreIds.OrderBy(id => id).ToArray(), queuedScoreIds.ToArray());
    }
}