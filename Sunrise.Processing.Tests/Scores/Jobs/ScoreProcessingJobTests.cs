using HOPEless.Bancho;
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