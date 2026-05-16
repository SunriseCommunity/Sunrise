using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Services;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using Mods = osu.Shared.Mods;

namespace Sunrise.Processing.Tests.Scores.Handlers;

[Collection("Integration tests collection")]
public class ScoreRecalculationHandlerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPrepareAsyncWithExistingScoreReturnsContextWithRecalculatedPerformance()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.Mods = Mods.None;
        score = await CreateTestScore(score);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(score);
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        using var scope = Scope;
        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 321);

        var handler = CreateHandler(scope);

        // Act
        var result = await handler.InvokePrepare(new ScoreTaskQueue
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
        Assert.Equal(score.BeatmapHash, result.Value.Beatmap!.Checksum);
    }

    [Fact]
    public async Task TestPrepareAsyncWithMissingScoreReturnsUnexpectedError()
    {
        // Arrange
        using var scope = Scope;
        var handler = CreateHandler(scope);

        // Act
        var result = await handler.InvokePrepare(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = 999_999
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
        Assert.Equal("Score 999999 not found", result.Error.Message);
    }

    [Fact]
    public async Task TestPrepareAsyncWithDeletedScoreReturnsUnexpectedError()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.SubmissionStatus = SubmissionStatus.Deleted;
        score.UserId = user.Id;
        score = await CreateTestScore(score);

        using var scope = Scope;
        var handler = CreateHandler(scope);

        // Act
        var result = await handler.InvokePrepare(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
        Assert.Equal($"Score {score.Id} is deleted; use RestoreScore to bring it back", result.Error.Message);
    }

    [Fact]
    public async Task TestPrepareAsyncWithMissingBeatmapReturnsBeatmapNotFound()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.BeatmapHash = "invalidhash";
        score.UserId = user.Id;
        score = await CreateTestScore(score);

        using var scope = Scope;
        var handler = CreateHandler(scope);

        // Act
        var result = await handler.InvokePrepare(new ScoreTaskQueue
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

    private TestScoreRecalculationHandler CreateHandler(IServiceScope scope)
    {
        return new TestScoreRecalculationHandler(
            scope.ServiceProvider.GetRequiredService<DatabaseService>(),
            new ScoreCommitPipeline(Database, []),
            scope.ServiceProvider.GetRequiredService<BeatmapService>(),
            scope.ServiceProvider.GetRequiredService<CalculatorService>());
    }

    private sealed class TestScoreRecalculationHandler(
        DatabaseService database,
        ScoreCommitPipeline pipeline,
        BeatmapService beatmapService,
        CalculatorService calculatorService)
        : ScoreRecalculationHandler(database, pipeline, beatmapService, calculatorService)
    {
        public Task<Result<ScoreCommitContext, ScoreProcessingError>> InvokePrepare(ScoreTaskQueue task, CancellationToken ct)
        {
            return PrepareAsync(task, ct);
        }
    }
}