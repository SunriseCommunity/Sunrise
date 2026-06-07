using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Calculators;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using Mods = osu.Shared.Mods;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Tests.Scores.Jobs;

[Collection("Integration tests collection")]
public class ScoreRecalculationProcessingJobTests(IntegrationDatabaseFixture fixture, bool reuseScopeInContext = true) : DatabaseTest(fixture, reuseScopeInContext)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestRecalculationOfDeletedScoreReturnsInvalidStateError()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.SubmissionStatus = SubmissionStatus.Deleted;
        score = await CreateTestScore(score);

        var handler = new ScoreRecalculationHandler(Database,
            CreatePipeline(),
            Scope.ServiceProvider.GetRequiredService<BeatmapService>(),
            Scope.ServiceProvider.GetRequiredService<CalculatorService>());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
    }

    [Fact]
    public async Task TestRecalculationOfMissingScoreReturnsUnexpectedError()
    {
        // Arrange
        var handler = new ScoreRecalculationHandler(Database,
            CreatePipeline(),
            Scope.ServiceProvider.GetRequiredService<BeatmapService>(),
            Scope.ServiceProvider.GetRequiredService<CalculatorService>());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = 999_999
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.Unexpected, result.Error.Code);
    }

    [Fact]
    public async Task TestRecalculationUpdatesPerformancePoints()
    {
        // Arrange
        var user = await CreateTestUser();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.PerformancePoints = 500;
        score.GameMode = GameMode.Standard;
        score.Mods = Mods.HardRock;
        score = await CreateTestScore(score);

        var (_, __) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: 100);

        var handler = new ScoreRecalculationHandler(Database,
            CreatePipeline(),
            Scope.ServiceProvider.GetRequiredService<BeatmapService>(),
            Scope.ServiceProvider.GetRequiredService<CalculatorService>());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        Database.DbContext.Entry(score).State = EntityState.Detached;
        var persistedScore = await Database.Scores.GetScore(score.Id, filterValidScores: false);
        Assert.NotNull(persistedScore);
        Assert.Equal(100, persistedScore.PerformancePoints);
    }

    [Fact]
    public async Task TestRecalculationUpdatesPerformancePointsAndUserStats()
    {
        // Arrange
        var user = await CreateTestUser();


        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.PerformancePoints = 500;
        score = await CreateTestScore(score);

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        Assert.NotNull(userStats);
        userStats.UpdateWithDbScore(score);
        await Database.Users.Stats.UpdateUserStats(userStats, user);

        var (_, __) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);

        const int expectedNewPerformancePoints = 300;

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: expectedNewPerformancePoints);

        var handler = new ScoreRecalculationHandler(Database,
            CreatePipeline(),
            Scope.ServiceProvider.GetRequiredService<BeatmapService>(),
            Scope.ServiceProvider.GetRequiredService<CalculatorService>());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
            },
            CancellationToken.None);

        Database.DbContext.ChangeTracker.Clear();

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.Id, filterValidScores: false);
        Assert.NotNull(persistedScore);
        Assert.Equal(expectedNewPerformancePoints, persistedScore.PerformancePoints);

        await Database.DbContext.Entry(userStats).ReloadAsync();

        var (expectedWeightedPerformancePoints, expectedWeightedAccuracy) = (PerformanceCalculator.CalculateUserWeightedPerformance([persistedScore]), PerformanceCalculator.CalculateUserWeightedAccuracy([persistedScore]));

        Assert.Equal(expectedWeightedPerformancePoints, userStats.PerformancePoints);
        Assert.Equal(expectedWeightedAccuracy, userStats.Accuracy);
    }

    [Fact]
    public async Task TestRecalculationReconcilesBestStatusWhenScoreBecomesHigher()
    {
        // Arrange
        var user = await CreateTestUser();

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var existingBest = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Best, "S", 500);
        var submittedScore = await CreatePersistedScore(user, beatmap, 800, SubmissionStatus.Submitted, "A", 400);

        const int expectedNewPerformancePoints = 300;

        App.MockHttpClient?.MockPerformanceCalculation(performancePoints: expectedNewPerformancePoints);

        var handler = new ScoreRecalculationHandler(Database,
            CreatePipeline(),
            Scope.ServiceProvider.GetRequiredService<BeatmapService>(),
            Scope.ServiceProvider.GetRequiredService<CalculatorService>());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = submittedScore.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedExistingBest = await Database.Scores.GetScore(existingBest.Id, new QueryOptions(true), false);
        Assert.NotNull(persistedExistingBest);
        Assert.Equal(SubmissionStatus.Best, persistedExistingBest.SubmissionStatus);

        var persistedSubmitted = await Database.Scores.GetScore(submittedScore.Id, new QueryOptions(true), false);
        Assert.NotNull(persistedSubmitted);
        Assert.Equal(SubmissionStatus.Submitted, persistedSubmitted.SubmissionStatus);
        Assert.Equal(expectedNewPerformancePoints, persistedSubmitted.PerformancePoints);
    }

    [Fact]
    public async Task TestRecalculationWithMissingBeatmapReturnsBeatmapNotFoundError()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);

        score.BeatmapHash = "missing-beatmap-hash";

        score = await CreateTestScore(score);

        var handler = new ScoreRecalculationHandler(Database,
            CreatePipeline(),
            Scope.ServiceProvider.GetRequiredService<BeatmapService>(),
            Scope.ServiceProvider.GetRequiredService<CalculatorService>());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Recalculation,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.BeatmapNotFound, result.Error.Code);
    }

    private ScoreCommitPipeline CreatePipeline()
    {
        return new ScoreCommitPipeline(Database,
        [
            new LeaderboardProcessor(Database),
            new UserGradesScoreProcessor(Database),
            new UserStatsScoreProcessor(Database, Scope.ServiceProvider.GetRequiredService<CalculatorService>())
        ]);
    }

    private async Task<Score> CreatePersistedScore(
        User user,
        Beatmap beatmap,
        long totalScore,
        SubmissionStatus submissionStatus,
        string grade,
        int maxCombo)
    {
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
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