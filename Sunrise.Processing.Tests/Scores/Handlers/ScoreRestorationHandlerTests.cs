using CSharpFunctionalExtensions;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Xunit;

namespace Sunrise.Processing.Tests.Scores.Handlers;

[Collection("Integration tests collection")]
public class ScoreRestorationHandlerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPrepareAsyncWithDeletedScoreReturnsRestoreContext()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.SubmissionStatus = SubmissionStatus.Deleted;
        score.UserId = user.Id;
        score = await CreateTestScore(score);

        var handler = CreateHandler();

        // Act
        var result = await handler.InvokePrepare(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Restore,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ScoreTaskType.Restore, result.Value.TaskType);
        Assert.Equal(score.Id, result.Value.Score.Id);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal(user.Id, result.Value.UserStats.UserId);
        Assert.Equal(user.Id, result.Value.UserGrades.UserId);
    }

    [Fact]
    public async Task TestPrepareAsyncWithMissingScoreReturnsUnexpectedError()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.InvokePrepare(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Restore,
                ScoreId = 999_999
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
        Assert.Equal("Score 999999 not found", result.Error.Message);
    }

    [Fact]
    public async Task TestPrepareAsyncWithActiveScoreReturnsUnexpectedError()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.SubmissionStatus = SubmissionStatus.Submitted;
        score.UserId = user.Id;
        score = await CreateTestScore(score);

        var handler = CreateHandler();

        // Act
        var result = await handler.InvokePrepare(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Restore,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
        Assert.Equal($"Score {score.Id} is not deleted", result.Error.Message);
    }

    private TestScoreRestorationHandler CreateHandler()
    {
        return new TestScoreRestorationHandler(Database, new ScoreCommitPipeline(Database, Array.Empty<IScoreEntityProcessor>()));
    }

    private sealed class TestScoreRestorationHandler(DatabaseService database, ScoreCommitPipeline pipeline) : ScoreRestorationHandler(database, pipeline)
    {
        public Task<Result<ScoreCommitContext, ScoreProcessingError>> InvokePrepare(ScoreTaskQueue task, CancellationToken ct)
        {
            return PrepareAsync(task, ct);
        }
    }
}