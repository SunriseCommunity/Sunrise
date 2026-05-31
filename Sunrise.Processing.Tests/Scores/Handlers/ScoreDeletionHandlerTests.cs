using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Tests.Scores.Handlers;

[Collection("Integration tests collection")]
public class ScoreDeletionHandlerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPrepareAsyncWithMissingScoreReturnsUnexpectedError()
    {
        // Arrange
        var handler = (ScoreDeletionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Delete);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = 999_999
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
    }

    [Fact]
    public async Task TestPrepareAsyncWithAlreadyDeletedScoreReturnsFailure()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        score.SubmissionStatus = SubmissionStatus.Deleted;

        score = await CreateTestScore(score);

        var handler = (ScoreDeletionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Delete);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
    }

    [Fact]
    public async Task TestPrepareAsyncWithValidScoreReturnsDeletionContext()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        score.SubmissionStatus = SubmissionStatus.Submitted;

        score = await CreateTestScore(score);

        var handler = (ScoreDeletionHandler)Scope.ServiceProvider
            .GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Delete);

        // Act
        var result = await handler.PrepareAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(score.Id, result.Value.Score.Id);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal(user.Id, result.Value.UserStats.UserId);
        Assert.Equal(user.Id, result.Value.UserGrades.UserId);
    }
}