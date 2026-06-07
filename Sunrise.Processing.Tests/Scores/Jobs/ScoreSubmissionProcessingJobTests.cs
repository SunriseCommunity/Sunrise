using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils.Processing;
using Xunit;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Tests.Scores.Jobs;

[Collection("Integration tests collection")]
public class ScoreSubmissionProcessingJobTests(IntegrationDatabaseFixture fixture, bool reuseScopeInContext = true) : DatabaseTest(fixture, reuseScopeInContext)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestSubmissionOfNewBestScorePersistsWithBestStatus()
    {
        // Arrange
        var (session, user) = await CreateTestSession();
        var (replay, beatmapId) = GetValidTestReplay();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var score = replay.GetScore();
        score.BeatmapId = beatmapId;
        score.EnrichWithSessionData(session);
        beatmapSet.Beatmaps!.First().EnrichWithScoreData(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        await Database.ScoreProcessingQueue.AddQueueEntry(queueEntry);

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 300);

        var handler = Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(persistedScore);
        Assert.Equal(user.Id, persistedScore.UserId);
        Assert.Equal(SubmissionStatus.Best, persistedScore.SubmissionStatus);
        Assert.Equal(300, persistedScore.PerformancePoints);
    }

    [Fact]
    public async Task TestSubmissionOfLowerScorePersistsWithSubmittedStatus()
    {
        // Arrange
        var (session, user) = await CreateTestSession();
        var (replay, beatmapId) = GetValidTestReplay();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();
        var score = replay.GetScore();
        score.BeatmapId = beatmapId;
        score.Mods = Mods.None;
        score.EnrichWithSessionData(session);
        score.EnrichWithBeatmapData(beatmap);
        beatmap.EnrichWithScoreData(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);


        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        await Database.ScoreProcessingQueue.AddQueueEntry(queueEntry);

        var existingBest = _mocker.Score.GetBestScoreableRandomScore();
        existingBest.EnrichWithUserData(user);
        existingBest.Mods = score.Mods;
        existingBest.EnrichWithBeatmapData(beatmap);

        existingBest.TotalScore = score.TotalScore + 1000;
        existingBest.SubmissionStatus = SubmissionStatus.Best;

        existingBest.LocalProperties = existingBest.LocalProperties.FromScore(existingBest);
        await CreateTestScore(existingBest);

        var handler = Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 100);

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(persistedScore);
        Assert.Equal(SubmissionStatus.Submitted, persistedScore.SubmissionStatus);
    }

    [Fact]
    public async Task TestSubmissionOfHigherScoreDemotesExistingBest()
    {
        // Arrange
        var (session, user) = await CreateTestSession();
        var (replay, beatmapId) = GetValidTestReplay();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();
        var score = replay.GetScore();
        score.BeatmapId = beatmapId;
        score.Mods = Mods.None;
        score.EnrichWithSessionData(session);


        score.TotalScore = 1000;

        beatmap.EnrichWithScoreData(score);
        score.EnrichWithBeatmapData(beatmap);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        await Database.ScoreProcessingQueue.AddQueueEntry(queueEntry);

        var existingBest = _mocker.Score.GetBestScoreableRandomScore();
        existingBest.EnrichWithUserData(user);
        existingBest.TotalScore = score.TotalScore - 100;
        existingBest.Mods = score.Mods;
        existingBest.EnrichWithBeatmapData(beatmap);
        existingBest.SubmissionStatus = SubmissionStatus.Best;
        existingBest.LocalProperties = existingBest.LocalProperties.FromScore(existingBest);
        existingBest = await CreateTestScore(existingBest);

        var handler = Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 500);

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedNewScore = await Database.Scores.GetScore(score.ScoreHash);
        var persistedOldBest = await Database.Scores.GetScore(existingBest.Id, filterValidScores: false);
        Assert.NotNull(persistedNewScore);
        Assert.NotNull(persistedOldBest);
        Assert.Equal(SubmissionStatus.Best, persistedNewScore.SubmissionStatus);
        Assert.Equal(SubmissionStatus.Submitted, persistedOldBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestSubmissionOfHigherScoreDoesntDemotesExistingDifferentGamemodeBest()
    {
        // Arrange
        var (session, user) = await CreateTestSession();
        var (replay, beatmapId) = GetValidTestReplay();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();
        var score = replay.GetScore();
        score.BeatmapId = beatmapId;
        score.EnrichWithSessionData(session);

        score.TotalScore = 999_999_999;

        score.GameMode = score.GameMode.EnrichWithMods(score.Mods);
        beatmap.EnrichWithScoreData(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);


        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        await Database.ScoreProcessingQueue.AddQueueEntry(queueEntry);

        // Create an existing best score with lower total score, but different gamemode.
        var existingBest = _mocker.Score.GetBestScoreableRandomScore();
        existingBest.EnrichWithUserData(user);
        existingBest.TotalScore = 100;
        existingBest.Mods = Mods.Relax;
        existingBest.EnrichWithBeatmapData(beatmap);
        existingBest.SubmissionStatus = SubmissionStatus.Best;
        existingBest.GameMode = existingBest.GameMode.EnrichWithMods(existingBest.Mods);
        existingBest.LocalProperties = existingBest.LocalProperties.FromScore(existingBest);
        existingBest = await CreateTestScore(existingBest);

        var handler = Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 500);

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedNewScore = await Database.Scores.GetScore(score.ScoreHash);
        var persistedOldBest = await Database.Scores.GetScore(existingBest.Id, filterValidScores: false);
        Assert.NotNull(persistedNewScore);
        Assert.NotNull(persistedOldBest);

        // Both should have their own best status 
        Assert.Equal(SubmissionStatus.Best, persistedNewScore.SubmissionStatus);
        Assert.Equal(SubmissionStatus.Best, persistedOldBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestSubmissionWithMissingBeatmapReturnsBeatmapNotFoundError()
    {
        // Arrange
        var (session, user) = await CreateTestSession();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.BeatmapHash = "nonexistent-hash";

        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        await Database.ScoreProcessingQueue.AddQueueEntry(queueEntry);

        var handler = Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.BeatmapNotFound, result.Error.Code);
    }

    [Fact]
    public async Task TestSubmissionOfDuplicateScoreHashReturnsDuplicateError()
    {
        // Arrange
        var (session, user) = await CreateTestSession();
        var (replay, beatmapId) = GetValidTestReplay();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var score = replay.GetScore();
        score.BeatmapId = beatmapId;
        score.EnrichWithSessionData(session);
        beatmapSet.Beatmaps!.First().EnrichWithScoreData(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        await Database.ScoreProcessingQueue.AddQueueEntry(queueEntry);

        var handler = Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 200);

        var firstResult = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        // Act
        var duplicateResult = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(duplicateResult.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.DuplicateScore, duplicateResult.Error.Code);
    }

    [Fact]
    public async Task TestSubmissionOfFailedScorePersistsWithFailedStatus()
    {
        // Arrange
        var (session, user) = await CreateTestSession();
        var (replay, beatmapId) = GetValidTestReplay();
        var score = replay.GetScore();
        score.BeatmapId = beatmapId;
        score.EnrichWithSessionData(session);
        score.IsPassed = false;
        score.Grade = "F";
        score.Mods = Mods.None;
        score.SubmissionStatus = SubmissionStatus.Failed;
        score.CountMiss = Math.Max(score.CountMiss, 1);
        score.LocalProperties = score.LocalProperties.FromScore(score);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(score);
        score.EnrichWithBeatmapData(beatmap);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: null);
        await Database.ScoreProcessingQueue.AddQueueEntry(queueEntry);

        var handler = Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 0);

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(persistedScore);
        Assert.False(persistedScore.IsPassed);
        Assert.Equal(SubmissionStatus.Failed, persistedScore.SubmissionStatus);
    }

    [Fact]
    public async Task TestSubmissionWithInvalidChecksumsRestrictsUserAndReturnsInvalidChecksums()
    {
        // Arrange
        var (session, user) = await CreateTestSession();
        var (replay, beatmapId) = GetValidTestReplay();
        var score = replay.GetScore();
        score.BeatmapId = beatmapId;
        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        queueEntry.UserHash = "other-user-hash";
        await Database.ScoreProcessingQueue.AddQueueEntry(queueEntry);

        var handler = Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);

        var refreshedUser = await Database.Users.GetUser(user.Id);
        Assert.NotNull(refreshedUser);
        Assert.Equal(UserAccountStatus.Restricted, refreshedUser.AccountStatus);
    }
}