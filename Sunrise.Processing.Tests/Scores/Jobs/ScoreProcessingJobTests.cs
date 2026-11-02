using HOPEless.Bancho;
using Sunrise.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Jobs;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils.Processing;
using Xunit;

namespace Sunrise.Processing.Tests.Scores.Jobs;

[Collection("Integration tests collection")]
public class ScoreProcessingJobTests(IntegrationDatabaseFixture fixture, bool reuseScopeInContext = true) : DatabaseTest(fixture, reuseScopeInContext)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestOwnershipLossCancelsTheWorker()
    {
        using var ownerScope = App.Services.CreateScope();
        var database = ownerScope.ServiceProvider.GetRequiredService<DatabaseService>();
        Assert.True(await database.ScoreProcessingTasks.TryAcquireQueueLock());
        try
        {
            await database.DbContext.Database.ExecuteSqlRawAsync("DO RELEASE_ALL_LOCKS()");
            Assert.False(await database.ScoreProcessingTasks.IsQueueLockHeld());
            using var ownershipCts = new CancellationTokenSource();
            var heartbeat = ScoreProcessingJob.MaintainQueueOwnership(database.ScoreProcessingTasks, ownershipCts, TimeSpan.FromMilliseconds(1));
            await Assert.ThrowsAsync<InvalidOperationException>(() => heartbeat.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(ownershipCts.IsCancellationRequested);
        }
        finally
        {
            await database.ScoreProcessingTasks.ReleaseQueueLock();
        }
    }

    [Fact]
    public async Task TestExpiredInFlightClaimIsExcludedButRecoverableAfterTheWorkerStops()
    {
        var score = await CreateTestScore();
        var queued = await CreateTask(ScoreTaskType.Recalculation, score.Id);
        var original = Assert.Single(await Database.ScoreProcessingTasks.ClaimPendingBatch(1, TimeSpan.FromMinutes(2)));
        await Database.DbContext.ScoreProcessingTasks.Where(t => t.Id == queued.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(-1)));

        Assert.Empty(await Database.ScoreProcessingTasks.ClaimPendingBatch(1, TimeSpan.FromMinutes(2), activeTaskIds: [queued.Id]));
        var recovered = Assert.Single(await Database.ScoreProcessingTasks.ClaimPendingBatch(1, TimeSpan.FromMinutes(2)));
        Assert.Equal(queued.Id, recovered.Id);
        Assert.NotEqual(original.ClaimToken, recovered.ClaimToken);
    }

    [Fact]
    public async Task TestHostedWorkerResumesAfterAnotherOwnerReleasesTheQueue()
    {
        var score = await CreateTestScore();
        var task = await CreateTask((ScoreTaskType)999, score.Id);
        using var ownerScope = App.Services.CreateScope();
        var owner = ownerScope.ServiceProvider.GetRequiredService<DatabaseService>().ScoreProcessingTasks;
        Assert.True(await owner.TryAcquireQueueLock());
        Assert.True(await owner.IsQueueLockHeld());
        var ownsQueue = true;
        using var worker = new ScoreProcessingWorker(App.Services.GetRequiredService<IServiceScopeFactory>());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await Scope.ServiceProvider.GetRequiredService<ScoreProcessingJob>().ProcessQueue(timeout.Token);
            Assert.Equal(0, (await Database.DbContext.ScoreProcessingTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id, timeout.Token)).RetryCount);
            await worker.StartAsync(timeout.Token);
            await owner.ReleaseQueueLock();
            ownsQueue = false;

            while (!await Database.DbContext.ScoreProcessingTasks.AsNoTracking().AnyAsync(t => t.Id == task.Id && t.RetryCount > 0, timeout.Token))
                await Task.Delay(25, timeout.Token);
        }
        finally
        {
            if (ownsQueue)
                await owner.ReleaseQueueLock();
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.Equal(1, (await Database.DbContext.ScoreProcessingTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id)).RetryCount);
    }

    [Fact]
    public async Task TestReleaseClaimOnlyReleasesTheMatchingOwnerWithoutCountingAFailure()
    {
        var score = await CreateTestScore();
        var queued = await CreateTask(ScoreTaskType.Recalculation, score.Id);
        var claimed = Assert.Single(await Database.ScoreProcessingTasks.ClaimPendingBatch(1, TimeSpan.FromMinutes(2)));

        await Database.ScoreProcessingTasks.ReleaseClaim(queued.Id, "another-owner");
        Assert.Equal(ScoreProcessingStatus.Processing,
            (await Database.DbContext.ScoreProcessingTasks.AsNoTracking().SingleAsync(t => t.Id == queued.Id)).Status);

        await Database.ScoreProcessingTasks.ReleaseClaim(queued.Id, claimed.ClaimToken!);
        var released = await Database.DbContext.ScoreProcessingTasks.AsNoTracking().SingleAsync(t => t.Id == queued.Id);
        Assert.Equal(ScoreProcessingStatus.Pending, released.Status);
        Assert.Equal(0, released.RetryCount);
        Assert.Null(released.ClaimToken);
        Assert.Null(released.LeaseExpiresAt);
    }

    [Fact]
    public async Task TestProcessQueueWithPermanentSubmissionFailureMarksTaskFailedAndNotifiesUser()
    {
        // Arrange
        var user = await CreateTestUser();
        var session = CreateTestSession(user);
        session.GetContent();

        var payload = new ScoreSubmissionRequest
        {
            UserId = user.Id,
            ScoreHash = $"{Guid.NewGuid():N}",
            ScoreSerialized = "unused",
            BeatmapHash = "missing-job-beatmap", // Beatmap that won't be found, causing a permanent failure
            TimeElapsed = 120,
            OsuVersion = "b20260101.1",
            ClientHash = "client-hash",
            UserHash = "user-hash",
            WhenPlayed = DateTime.UtcNow
        };

        await Database.ScoreSubmissionRequests.AddQueueEntry(payload);

        var task = await CreateTask(ScoreTaskType.Submission, scoreSubmissionRequestId: payload.Id);

        var job = Scope.ServiceProvider.GetRequiredService<ScoreProcessingJob>();

        // Act
        await job.ProcessQueue(CancellationToken.None);

        // Assert
        var refreshedTask = await Database.DbContext.ScoreProcessingTasks.AsNoTracking().SingleAsync(x => x.Id == task.Id);
        Assert.Equal(ScoreProcessingStatus.Failed, refreshedTask.Status);
        Assert.Equal(ScoreProcessingErrorCode.BeatmapNotFound, refreshedTask.ErrorCode);
        Assert.Null(refreshedTask.NextRetryAt);
        Assert.Equal(1, refreshedTask.RetryCount);

        var notificationPacket = GetSessionPackets(session).FirstOrDefault(packet => packet.Type == PacketType.ServerNotification);
        Assert.NotNull(notificationPacket);
    }

    [Fact]
    public async Task TestProcessQueueWithRetryableSubmissionFailureRequeuesTask()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = await CreateTestScore(user);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(score);

        var replayFileId = await CreateReplayFileId(user.Id);
        var payload = ScoreSubmissionRequestTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        await Database.ScoreSubmissionRequests.AddQueueEntry(payload);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        App.MockHttpClient?.MockResponse<PerformanceAttributes>(ApiType.CalculateScorePerformance, _ => throw new Exception("pp failed")); // Simulate a failure in performance calculation, which should be treated as a retryable error

        var task = await CreateTask(ScoreTaskType.Submission, scoreSubmissionRequestId: payload.Id);
        var job = Scope.ServiceProvider.GetRequiredService<ScoreProcessingJob>();

        // Act
        await job.ProcessQueue(CancellationToken.None);

        // Assert
        var refreshedTask = await Database.DbContext.ScoreProcessingTasks.AsNoTracking().SingleAsync(x => x.Id == task.Id);
        Assert.Equal(ScoreProcessingStatus.Pending, refreshedTask.Status);
        Assert.Equal(ScoreProcessingErrorCode.PpCalculationFailed, refreshedTask.ErrorCode);
        Assert.NotNull(refreshedTask.NextRetryAt);
        Assert.Equal(1, refreshedTask.RetryCount);

        var refreshedPayload = await Database.ScoreSubmissionRequests.GetById(payload.Id);
        Assert.NotNull(refreshedPayload);
    }

    [Fact]
    public async Task TestProcessQueueWithDuplicateSubmissionCleansUpTaskAndPayloadWithoutCreatingSecondScore()
    {
        // Arrange
        var user = await CreateTestUser();
        var replayFileId = await CreateReplayFileId(user.Id);
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.ReplayFileId = replayFileId;

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(score);

        var payload = ScoreSubmissionRequestTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        score.ScoreHash = payload.ScoreHash;
        score = await CreateTestScore(score);

        await Database.ScoreSubmissionRequests.AddQueueEntry(payload);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 200);

        var task = await CreateTask(ScoreTaskType.Submission, scoreSubmissionRequestId: payload.Id);
        var job = Scope.ServiceProvider.GetRequiredService<ScoreProcessingJob>();

        // Act
        await job.ProcessQueue(CancellationToken.None);

        Database.DbContext.ChangeTracker.Clear();

        // Assert
        Assert.Null(await Database.DbContext.ScoreProcessingTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == task.Id));
        Assert.Null(await Database.ScoreSubmissionRequests.GetById(payload.Id));

        var persistedScore = await Database.Scores.GetScore(score.Id, filterValidScores: false);
        Assert.NotNull(persistedScore);
        Assert.Equal(payload.ScoreHash, persistedScore.ScoreHash);
        Assert.Equal(SubmissionStatus.Best, persistedScore.SubmissionStatus);
        Assert.Equal(1, await Database.DbContext.Scores.AsNoTracking().CountAsync(x => x.UserId == user.Id));
    }

    [Fact]
    public async Task TestProcessQueueWithUnexpectedHandlerResolutionFailureMarksTaskAsUnexpected()
    {
        // Arrange
        var score = await CreateTestScore();
        var task = await CreateTask((ScoreTaskType)999, score.Id);

        var job = Scope.ServiceProvider.GetRequiredService<ScoreProcessingJob>();

        // Act
        await job.ProcessQueue(CancellationToken.None);

        // Assert
        var refreshedTask = await Database.DbContext.ScoreProcessingTasks.AsNoTracking().SingleAsync(x => x.Id == task.Id);
        Assert.Equal(ScoreProcessingStatus.Pending, refreshedTask.Status);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, refreshedTask.ErrorCode);
        Assert.NotNull(refreshedTask.NextRetryAt);
        Assert.Equal(1, refreshedTask.RetryCount);
    }

    private async Task<ScoreProcessingTask> CreateTask(ScoreTaskType taskType, int? scoreId = null, int? scoreSubmissionRequestId = null)
    {
        var task = new ScoreProcessingTask
        {
            TaskType = taskType,
            ScoreId = scoreId,
            ScoreSubmissionRequestId = scoreSubmissionRequestId,
            CreatedAt = DateTime.UtcNow
        };

        await Database.ScoreProcessingTasks.AddQueueEntry(task);
        return task;
    }

    private static List<BanchoPacket> GetSessionPackets(Session session)
    {
        var content = session.GetContent();
        using var buffer = new MemoryStream(content);
        return BanchoSerializer.DeserializePackets(buffer).ToList();
    }
}
