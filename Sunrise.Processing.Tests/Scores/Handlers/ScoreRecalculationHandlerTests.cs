using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;

namespace Sunrise.Processing.Tests.Scores.Handlers;

[Collection("Integration tests collection")]
public class ScoreRecalculationHandlerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPrepareAsyncWithMissingScoreReturnsUnexpectedError()
    {
        // Arrange
        var handler = (ScoreRecalculationHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Recalculation);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = 999_999
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithDeletedScoreReturnsUnexpectedError()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        score.SubmissionStatus = SubmissionStatus.Deleted;

        score = await CreateTestScore(score);

        var handler = (ScoreRecalculationHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Recalculation);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithServerErrorResponseForBeatmapReturnsBeatmapNotFoundRetryable()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score = await CreateTestScore(score);

        App.MockHttpClient?.MockBeatmapSetByHashInternalServerError();

        var handler = (ScoreRecalculationHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Recalculation);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
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
        score = await CreateTestScore(score);

        App.MockHttpClient?.MockBeatmapSetByBeatmapIdNotFound(score.BeatmapId);

        var handler = (ScoreRecalculationHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Recalculation);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.BeatmapNotFound, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithFailedPpCalculationReturnsPpCalculationFailed()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score = await CreateTestScore(score);

        await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);

        var handler = (ScoreRecalculationHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Recalculation);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.PpCalculationFailed, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Retryable, result.Error.Disposition);
    }

    [Fact]
    public async Task TestPrepareAsyncWithExistingScoreReturnsContextWithRecalculatedPerformance()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        score.PerformancePoints = 123;

        score = await CreateTestScore(score);

        var (_, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 321);

        var handler = (ScoreRecalculationHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Recalculation);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        Assert.Equal(ScoreTaskType.Recalculation, result.Value.TaskType);
        Assert.Equal(score.Id, result.Value.Score.Id);
        Assert.Equal(321, result.Value.Score.PerformancePoints);

        Assert.NotNull(result.Value.Beatmap);
        Assert.Equal(beatmap.Checksum, result.Value.Beatmap!.Checksum);
    }
}