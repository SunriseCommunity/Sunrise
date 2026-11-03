using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Server.Commands.ChatCommands.System;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.Commands.ChatCommands.System;

[Collection("Integration tests collection")]
public class RecalculateScoresCommandTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture, true)
{
    [Fact]
    public async Task QueuesMatchingScoresAcrossChunksWithoutResettingActiveTasks()
    {
        var scores = await CreateScores(1007);
        await Database.DbContext.Scores.Where(s => s.Id == scores[2].Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.GameMode, GameMode.Mania));
        await Database.DbContext.Scores.Where(s => s.Id == scores[3].Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SubmissionStatus, SubmissionStatus.Deleted));
        await Database.DbContext.Scores.Where(s => s.Id == scores[4].Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SubmissionStatus, SubmissionStatus.Unknown));
        var active = new ScoreProcessingTask
        {
            ScoreId = scores[501].Id,
            TaskType = ScoreTaskType.Delete,
            Status = ScoreProcessingStatus.Processing,
            RetryCount = 4,
            Priority = (int)ScoreProcessingPriority.High,
            ClaimToken = "existing-owner",
            CreatedAt = DateTime.UtcNow
        };
        await Database.ScoreProcessingTasks.AddQueueEntry(active);
        await Database.ScoreProcessingTasks.AddQueueEntry(new ScoreProcessingTask
        {
            ScoreId = scores[600].Id,
            TaskType = ScoreTaskType.Recalculation,
            Status = ScoreProcessingStatus.Failed,
            CreatedAt = DateTime.UtcNow
        });
        var existingFirstPage = scores.Skip(5).Prepend(scores[1]).Take(500).Select(s => s.Id).ToList();
        await Database.ScoreProcessingTasks.EnqueueScoreTasks(existingFirstPage, ScoreTaskType.Recalculation, ScoreProcessingPriority.Low);

        using var sql = new SqlCommandCapture();
        await new RecalculateScoresCommand().RecalculateScores(scores[0].UserId, CancellationToken.None, scores[1].Id, GameMode.Standard);

        var tasks = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().ToListAsync();
        var queued = tasks.Where(t => t.TaskType == ScoreTaskType.Recalculation && t.Status == ScoreProcessingStatus.Pending).ToList();
        var expected = scores.Skip(5).Append(scores[1]).Where(s => s.Id != active.ScoreId).Select(s => s.Id).OrderBy(id => id);
        Assert.Equal(expected, queued.Select(t => t.ScoreId!.Value).OrderBy(id => id));
        Assert.All(queued, t =>
        {
            Assert.Equal((int)ScoreProcessingPriority.Low, t.Priority);
            Assert.Equal(0, t.RetryCount);
            Assert.Null(t.RunId);
        });
        var unchanged = Assert.Single(tasks, t => t.Id == active.Id);
        Assert.Equal(4, unchanged.RetryCount);
        Assert.Equal(ScoreProcessingStatus.Processing, unchanged.Status);
        Assert.Equal("existing-owner", unchanged.ClaimToken);
        Assert.Equal((int)ScoreProcessingPriority.High, unchanged.Priority);
        Assert.DoesNotContain(sql.Commands, command => command.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(sql.Commands.Count(command => command.Contains("INSERT IGNORE INTO score_processing_task")), 2, 4);
    }

    [Fact]
    public async Task ConcurrentProducersSkipOverlappingAndMissingTargets()
    {
        var scores = await CreateScores(503);
        var ids = scores.Select(s => s.Id).ToList();
        ids.Add(ids[0]);
        ids.Add(int.MaxValue);
        using var firstScope = App.Services.CreateScope();
        using var secondScope = App.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<DatabaseService>();
        var second = secondScope.ServiceProvider.GetRequiredService<DatabaseService>();

        var counts = await Task.WhenAll(
            first.ScoreProcessingTasks.EnqueueScoreTasks(ids, ScoreTaskType.Recalculation, ScoreProcessingPriority.Low),
            second.ScoreProcessingTasks.EnqueueScoreTasks(ids.AsEnumerable().Reverse().ToList(), ScoreTaskType.Recalculation, ScoreProcessingPriority.Low));

        Assert.Equal(scores.Count, counts.Sum());
        var queuedIds = await Database.DbContext.ScoreProcessingTasks.NotCacheable().Select(t => t.ScoreId!.Value).OrderBy(id => id).ToListAsync();
        Assert.Equal(scores.Select(s => s.Id).OrderBy(id => id), queuedIds);
    }

    [Fact]
    public async Task CancelledCommandDoesNotQueueAnyScores()
    {
        var scores = await CreateScores(1);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await new RecalculateScoresCommand().RecalculateScores(scores[0].UserId, cancellation.Token, 0);

        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync());
    }

    private async Task<List<Score>> CreateScores(int count)
    {
        var mocker = new MockService();
        var user = await CreateTestUser();
        var first = mocker.Score.GetBestScoreableRandomScore();
        first.UserId = user.Id;
        first.GameMode = GameMode.Standard;
        await CreateTestScore(first);
        var scores = new List<Score> { first };
        for (var i = 1; i < count; i++)
        {
            var score = mocker.Score.GetBestScoreableRandomScore();
            score.Id = first.Id + i * 3;
            score.UserId = user.Id;
            score.GameMode = GameMode.Standard;
            score.BeatmapHash = first.BeatmapHash;
            score.BeatmapHashStatus = null;
            score.ReplayFileId = null;
            scores.Add(score);
        }

        Database.DbContext.Scores.AddRange(scores.Skip(1));
        await Database.DbContext.SaveChangesAsync();
        return scores;
    }
}
