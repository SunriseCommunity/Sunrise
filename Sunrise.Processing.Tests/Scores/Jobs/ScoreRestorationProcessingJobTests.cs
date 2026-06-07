using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
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
public class ScoreRestorationProcessingJobTests(IntegrationDatabaseFixture fixture, bool reuseScopeInContext = true) : DatabaseTest(fixture, reuseScopeInContext)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestRestorationOfMissingScoreReturnsUnexpectedError()
    {
        // Arrange
        var handler = new ScoreRestorationHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
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
    public async Task TestRestorationOfNonDeletedScoreReturnsInvalidStateError()
    {
        // Arrange
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var score = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Best, "S", 500);

        var handler = new ScoreRestorationHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
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
    public async Task TestRestorationOfDeletedScoreWithNoPeersSetsBestStatus()
    {
        // Arrange
        var user = await CreateTestUser();
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var score = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Deleted, "S", 500);

        var handler = new ScoreRestorationHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Restore,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.Id, filterValidScores: false);
        Assert.NotNull(persistedScore);
        Assert.Equal(SubmissionStatus.Best, persistedScore.SubmissionStatus);
    }

    [Fact]
    public async Task TestRestorationOfBetterDeletedScoreDemotesSameGamemodePeerAndUpdatesGameStatsAndGrades()
    {
        // Arrange
        var user = await CreateTestUser();

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var peerReplacement = await CreatePersistedScore(user, beatmap, 900, SubmissionStatus.Best, "S", 450);

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, peerReplacement.GameMode);
        Assert.NotNull(userStats);

        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, peerReplacement.GameMode);
        Assert.NotNull(userGrades);

        userStats.UpdateWithDbScore(peerReplacement);
        userGrades.UpdateWithDbScore(peerReplacement);

        var score = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Deleted, "A", 500);

        userStats.UpdateWithDbScore(score);
        userGrades.UpdateWithDbScore(score);

        await Database.Users.Stats.UpdateUserStats(userStats, user);
        await Database.Users.Grades.UpdateUserGrades(userGrades);

        var handler = new ScoreRestorationHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Restore,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.Id, filterValidScores: false);
        Assert.NotNull(persistedScore);
        Assert.Equal(SubmissionStatus.Best, persistedScore.SubmissionStatus);

        var persistedPeerReplacement = await Database.Scores.GetScore(peerReplacement.Id, filterValidScores: false);
        Assert.NotNull(persistedPeerReplacement);
        Assert.Equal(SubmissionStatus.Submitted, persistedPeerReplacement.SubmissionStatus);

        await Database.DbContext.Entry(userStats).ReloadAsync();
        await Database.DbContext.Entry(userGrades).ReloadAsync();

        Assert.Equal(1, userGrades.GetGradeCount(score.Grade));
        Assert.Equal(0, userGrades.GetGradeCount(peerReplacement.Grade));

        var (expectedWeightedPerformancePoints, expectedWeightedAccuracy) = (PerformanceCalculator.CalculateUserWeightedPerformance([score]), PerformanceCalculator.CalculateUserWeightedAccuracy([score]));

        Assert.Equal(score.TotalScore + peerReplacement.TotalScore, userStats.TotalScore);
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);
        Assert.Equal(score.TotalScore, userStats.RankedScore);
        Assert.Equal(expectedWeightedPerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(expectedWeightedAccuracy, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestRestorationOfHigherScoreDemotesExistingBest()
    {
        // Arrange
        var user = await CreateTestUser();
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var existingWithLowerScoreBest = await CreatePersistedScore(user, beatmap, 500, SubmissionStatus.Best, "A", 300);
        var deletedWithHigherScoreScore = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Deleted, "S", 500);

        var handler = new ScoreRestorationHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Restore,
                ScoreId = deletedWithHigherScoreScore.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedDeletedWithHigherScoreScore = await Database.Scores.GetScore(deletedWithHigherScoreScore.Id, filterValidScores: false);
        Assert.NotNull(persistedDeletedWithHigherScoreScore);
        Assert.Equal(SubmissionStatus.Best, persistedDeletedWithHigherScoreScore.SubmissionStatus);

        var persistedExistingWithLowerScoreBest = await Database.Scores.GetScore(existingWithLowerScoreBest.Id, filterValidScores: false);
        Assert.NotNull(persistedExistingWithLowerScoreBest);
        Assert.Equal(SubmissionStatus.Submitted, persistedExistingWithLowerScoreBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestRestorationOfLowerScoreKeepsExistingBest()
    {
        // Arrange
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var existingWithHigherScoreBest = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Best, "S", 500);
        var deletedScoreWithLowerScore = await CreatePersistedScore(user, beatmap, 500, SubmissionStatus.Deleted, "A", 300);

        var handler = new ScoreRestorationHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Restore,
                ScoreId = deletedScoreWithLowerScore.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedDeletedWithLowerScore = await Database.Scores.GetScore(deletedScoreWithLowerScore.Id, new QueryOptions(true), false);
        Assert.NotNull(persistedDeletedWithLowerScore);
        Assert.Equal(SubmissionStatus.Submitted, persistedDeletedWithLowerScore.SubmissionStatus);

        var persistedExistingWithHigherScoreBest = await Database.Scores.GetScore(existingWithHigherScoreBest.Id, new QueryOptions(true), false);
        Assert.NotNull(persistedExistingWithHigherScoreBest);
        Assert.Equal(SubmissionStatus.Best, persistedExistingWithHigherScoreBest.SubmissionStatus);
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