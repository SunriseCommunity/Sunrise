using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Services;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using Mods = osu.Shared.Mods;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Tests.Scores.Handlers;

[Collection("Integration tests collection")]
public class ScoreDeletionHandlerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestExecuteAsyncWithExistingBestScoreDeletesScoreAndPromotesReplacement()
    {
        // Arrange
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var replacement = await CreatePersistedScore(user.Id, beatmap, 900, SubmissionStatus.Submitted, "S", 450);
        var score = await CreatePersistedScore(user.Id, beatmap, 1000, SubmissionStatus.Best, "A", 500);

        using var scope = Scope;
        var handler = CreateHandler(scope);
        var task = new ScoreTaskQueue
        {
            TaskType = ScoreTaskType.Delete,
            ScoreId = score.Id
        };

        // Act
        var result = await handler.ExecuteAsync(task, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.Id, filterValidScores: false);
        var persistedReplacement = await Database.Scores.GetScore(replacement.Id, filterValidScores: false);
        Assert.NotNull(persistedScore);
        Assert.NotNull(persistedReplacement);
        Assert.Equal(SubmissionStatus.Deleted, persistedScore.SubmissionStatus);
        Assert.Equal(SubmissionStatus.Best, persistedReplacement.SubmissionStatus);
    }

    [Fact]
    public async Task TestExecuteAsyncWithMissingScoreReturnsUnexpectedError()
    {
        // Arrange
        using var scope = Scope;
        var handler = CreateHandler(scope);
        var task = new ScoreTaskQueue
        {
            TaskType = ScoreTaskType.Delete,
            ScoreId = 999_999
        };

        // Act
        var result = await handler.ExecuteAsync(task, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
        Assert.Equal("Score 999999 not found", result.Error.Message);
    }

    [Fact]
    public async Task TestExecuteAsyncWithAlreadyDeletedScoreReturnsFailure()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.SubmissionStatus = SubmissionStatus.Deleted;
        score.UserId = user.Id;
        score = await CreateTestScore(score);

        using var scope = Scope;
        var handler = CreateHandler(scope);
        var task = new ScoreTaskQueue
        {
            TaskType = ScoreTaskType.Delete,
            ScoreId = score.Id
        };

        // Act
        var result = await handler.ExecuteAsync(task, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
        Assert.Equal($"Score {score.Id} is already deleted", result.Error.Message);
    }

    private static ScoreDeletionHandler CreateHandler(IServiceScope scope)
    {
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DatabaseService>();
        return new ScoreDeletionHandler(database, CreatePipeline(services));
    }

    private static ScoreCommitPipeline CreatePipeline(IServiceProvider services)
    {
        var database = services.GetRequiredService<DatabaseService>();

        return new ScoreCommitPipeline(database,
        [
            new LeaderboardProcessor(database),
            new UserGradesScoreProcessor(database),
            new UserStatsScoreProcessor(database, services.GetRequiredService<CalculatorService>())
        ]);
    }

    private async Task<Score> CreatePersistedScore(
        int userId,
        Beatmap beatmap,
        long totalScore,
        SubmissionStatus submissionStatus,
        string grade,
        int maxCombo)
    {
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = userId;
        score.Mods = Mods.None;
        score.TotalScore = totalScore;
        score.Grade = grade;
        score.MaxCombo = maxCombo;
        score.EnrichWithBeatmapData(beatmap);
        score.SubmissionStatus = submissionStatus;
        score.LocalProperties = score.LocalProperties.FromScore(score);

        return await CreateTestScore(score);
    }
}