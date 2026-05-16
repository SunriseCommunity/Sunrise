using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils.Processing;
using Xunit;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Tests.Scores.Handlers;

[Collection("Integration tests collection")]
public class ScoreSubmissionHandlerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestExecuteAsyncWithMissingPayloadReferenceReturnsUnexpectedError()
    {
        // Arrange
        using var scope = Scope;
        var handler = scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
        Assert.Equal("Submission task 0 is missing its payload reference", result.Error.Message);
    }

    [Fact]
    public async Task TestExecuteAsyncWithMissingPayloadReturnsUnexpectedError()
    {
        // Arrange
        using var scope = Scope;
        var handler = scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Submission,
                ScoreProcessingQueueId = 999_999
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
        Assert.Equal("Submission payload 999999 was not found for task 0", result.Error.Message);
    }

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

        using var scope = Scope;
        var handler = scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>();
        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 250);

        // Act
        var result = await handler.ProcessInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);

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
        var result = await handler.ProcessInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);

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

        var initialResult = await handler.ProcessInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);
        Assert.True(initialResult.IsSuccess);

        // Act
        var duplicateResult = await handler.ProcessInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);

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
        var result = await handler.ProcessInlineSubmission(BaseSession.GenerateServerSession(), queueEntry, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);

        var refreshedUser = await Database.Users.GetUser(user.Id);
        Assert.NotNull(refreshedUser);
        Assert.Equal(UserAccountStatus.Restricted, refreshedUser.AccountStatus);
    }

    private async Task<int> CreateReplayFileId(int userId)
    {
        IFormFile replayFile = new FormFile(new MemoryStream(new byte[1024]), 0, 1024, "data", "score.osr");
        var replayResult = await Database.Scores.Files.AddReplayFile(userId, replayFile);

        Assert.True(replayResult.IsSuccess);
        return replayResult.Value.Id;
    }
}