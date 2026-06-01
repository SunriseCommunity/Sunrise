using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils.Processing;
using Xunit;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Tests.Scores.Handlers;

[Collection("Integration tests collection")]
public class ScoreSubmissionHandlerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPrepareAsyncWithMissingPayloadReferenceReturnsUnexpectedError()
    {
        // Arrange
        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
    }

    [Fact]
    public async Task TestPrepareAsyncWithMissingPayloadReturnsUnexpectedError()
    {
        // Arrange
        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = 999_999
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
    }


    [Fact]
    public async Task TestPrepareAsyncWithServerErrorResponseForBeatmapReturnsBeatmapNotFoundRetryable()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        var queueEntry = await CreateTestScoreProcessingQueue(score, user);

        App.MockHttpClient?.MockBeatmapSetByHashInternalServerError();

        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.BeatmapNotFound, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Retryable, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithMissingBeatmapReturnsBeatmapNotFoundPermanent()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        var queueEntry = await CreateTestScoreProcessingQueue(score, user);

        App.MockHttpClient?.MockBeatmapSetByBeatmapIdNotFound(score.BeatmapId);

        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.BeatmapNotFound, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithMissingReplayReturnsReplayMissing()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        var queueEntry = await CreateTestScoreProcessingQueue(score, user, false);

        await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);

        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.ReplayMissing, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
    }

    [Theory]
    [InlineData(Mods.DoubleTime | Mods.HalfTime, ScoreProcessingErrorCode.InvalidMods)]
    [InlineData(Mods.Relax | Mods.Relax2, ScoreProcessingErrorCode.InvalidMods)]
    [InlineData(Mods.Target, ScoreProcessingErrorCode.InvalidMods)]
    [InlineData(Mods.Key1, ScoreProcessingErrorCode.InvalidMods, GameMode.Standard)]
    public async Task TestPrepareAsyncWithInvalidModsReturnsInvalidMods(Mods mods, ScoreProcessingErrorCode expectedErrorCode, GameMode? gamemodeOverride = null)
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        score.Mods = mods;

        if (gamemodeOverride.HasValue)
            score.GameMode = gamemodeOverride.Value;

        score.GameMode.EnrichWithMods(score.Mods);

        var queueEntry = await CreateTestScoreProcessingQueue(score, user);

        await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);

        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(expectedErrorCode, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithInvalidChecksumsReturnsInvalidChecksums()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);

        queueEntry.ClientHash = "invalid-client-hash";
        queueEntry.ScoreHash = "invalid-score-hash";

        await Database.ScoreProcessingQueue.AddQueueEntry(queueEntry);

        await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);

        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithFailedPpCalculationReturnsPpCalculationFailed()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        var queueEntry = await CreateTestScoreProcessingQueue(score, user);

        await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);

        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.PpCalculationFailed, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Retryable, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithPpCalculationBeyondBannableThresholdReturnsBannablePpThreshold()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.Standard;
        score.Mods = Mods.None;
        score.EnrichWithUserData(user);
        var queueEntry = await CreateTestScoreProcessingQueue(score, user);

        await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 999999);

        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.BannablePpThreshold, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithSubmissionScoreProcessingQueueEntryReturnsSubmissionContext()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        var queueEntry = await CreateTestScoreProcessingQueue(score, user);

        await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        App.MockHttpClient?.MockPerformanceCalculation();

        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Restore,
                ScoreProcessingQueueId = queueEntry.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ScoreTaskType.Submission, result.Value.TaskType);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal(user.Id, result.Value.UserStats.UserId);
        Assert.Equal(user.Id, result.Value.UserGrades.UserId);
    }

    [Fact]
    public async Task TestOnCommittedWithSubmissionScoreProcessingQueueEntryAchievesMedals()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.GameMode = GameMode.Standard;
        score.Mods = Mods.DoubleTime;
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, GameMode.Standard);
        Assert.NotNull(userGrades);
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);

        var ctx = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap, beatmapSet);

        await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        App.MockHttpClient?.MockPerformanceCalculation();


        var handler = (ScoreSubmissionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Submission);

        // Act
        await handler.OnCommitted(ctx, CancellationToken.None);

        // Assert
        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id, GameMode.Standard);

        Assert.NotNull(userMedals);
        Assert.NotNull(userMedals.FirstOrDefault(m => m.MedalId == 92)); // Intro Medal for the DoubleTime mod
    }
}

[Collection("Integration tests collection")]
public class ScoreSubmissionInlineHandlerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    // TODO: Fill with more tests

    [Fact]
    public async Task TestProcessInlineSubmissionWithValidScoreReturnsResponseAndPersistsScore()
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

        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var handler = Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();
        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 250);

        // Act
        var result = await handler.ExecuteInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var persistedScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(persistedScore);
        Assert.Equal(user.Id, persistedScore.UserId);
        Assert.Equal(250, persistedScore.PerformancePoints);
    }

    [Fact]
    public async Task TestProcessInlineSubmissionWithFailedScoreReturnsSuccessWithNullResponse()
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

        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: null);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        using var scope = Scope;
        var handler = scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();
        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 25);

        // Act
        var result = await handler.ExecuteInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);

        var persistedScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(persistedScore);
        Assert.False(persistedScore.IsPassed);
    }

    [Fact]
    public async Task TestProcessInlineSubmissionWithDuplicateScoreReturnsDuplicateScoreError()
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

        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        using var scope = Scope;
        var handler = scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();
        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 200);

        var initialResult = await handler.ExecuteInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);
        Assert.True(initialResult.IsSuccess);

        // Act
        var duplicateResult = await handler.ExecuteInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);

        // Assert
        Assert.True(duplicateResult.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.DuplicateScore, duplicateResult.Error.Code);
        Assert.Equal("Score with same hash already exists", duplicateResult.Error.Message);
    }

    [Fact]
    public async Task TestProcessInlineSubmissionWithInvalidChecksumsRestrictsUserAndReturnsInvalidChecksums()
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

        var replayFileId = await CreateReplayFileId(user.Id);
        var queueEntry = ScoreProcessingTestDataFactory.CreateQueueEntry(score, user.Username, replayFileId: replayFileId);
        queueEntry.UserHash = "other-user-hash";

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        using var scope = Scope;
        var handler = scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        // Act
        var result = await handler.ExecuteInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);

        var refreshedUser = await Database.Users.GetUser(user.Id);
        Assert.NotNull(refreshedUser);
        Assert.Equal(UserAccountStatus.Restricted, refreshedUser.AccountStatus);
    }
}