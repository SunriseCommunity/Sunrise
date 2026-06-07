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
public class ScoreRestorationHandlerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPrepareAsyncWithMissingScoreReturnsUnexpectedError()
    {
        // Arrange
        var handler = (ScoreRestorationHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Restore);

        // Act
        var result = await handler.PrepareAsync(new ScoreProcessingTask
            {
                TaskType = ScoreTaskType.Restore,
                ScoreId = 999_999
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
    }

    [Fact]
    public async Task TestPrepareAsyncWithActiveScoreReturnsUnexpectedError()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        score.SubmissionStatus = SubmissionStatus.Submitted;

        score = await CreateTestScore(score);

        var handler = (ScoreRestorationHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Restore);

        // Act
        var result = await handler.PrepareAsync(new ScoreProcessingTask
            {
                TaskType = ScoreTaskType.Restore,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
    }

    [Fact]
    public async Task TestPrepareAsyncWithDeletedScoreReturnsRestoreContext()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        score.SubmissionStatus = SubmissionStatus.Deleted;

        score = await CreateTestScore(score);

        var handler = (ScoreRestorationHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Restore);

        // Act
        var result = await handler.PrepareAsync(new ScoreProcessingTask
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
}