using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Services;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using Mods = osu.Shared.Mods;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Tests.Scores.Jobs;

[Collection("Integration tests collection")]
public class ScoreDeletionProcessingJobTests(IntegrationDatabaseFixture fixture, bool reuseScopeInContext = true) : DatabaseTest(fixture, reuseScopeInContext)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestDeletionWithMissingScoreReturnsError()
    {
        // Arrange
        var handler = new ScoreDeletionHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
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
    public async Task TestDeletionOfAlreadyDeletedScoreReturnsInvalidStateError()
    {
        // Arrange
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();

        score.SubmissionStatus = SubmissionStatus.Deleted;

        score.EnrichWithUserData(user);
        score = await CreateTestScore(score);

        var handler = new ScoreDeletionHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
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
    public async Task TestDeletionOfBestScorePromotesSameGamemodePeerAndUpdatesGameStatsAndGrades()
    {
        // Arrange
        var user = await CreateTestUser();

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var peerReplacement = await CreatePersistedScore(user, beatmap, 900, SubmissionStatus.Submitted, "S", 450);

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, peerReplacement.GameMode);
        Assert.NotNull(userStats);

        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, peerReplacement.GameMode);
        Assert.NotNull(userGrades);

        userStats.UpdateWithDbScore(peerReplacement);
        userGrades.UpdateWithDbScore(peerReplacement);

        var score = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Best, "A", 500);

        userStats.UpdateWithDbScore(score);
        userGrades.UpdateWithDbScore(score);

        await Database.Users.Stats.UpdateUserStats(userStats, user);
        await Database.Users.Grades.UpdateUserGrades(userGrades);

        var handler = new ScoreDeletionHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.Id, filterValidScores: false);
        Assert.NotNull(persistedScore);
        Assert.Equal(SubmissionStatus.Deleted, persistedScore.SubmissionStatus);

        var persistedPeerReplacement = await Database.Scores.GetScore(peerReplacement.Id, filterValidScores: false);
        Assert.NotNull(persistedPeerReplacement);
        Assert.Equal(SubmissionStatus.Best, persistedPeerReplacement.SubmissionStatus);

        await Database.DbContext.Entry(userStats).ReloadAsync();
        await Database.DbContext.Entry(userGrades).ReloadAsync();

        Assert.Equal(0, userGrades.GetGradeCount(score.Grade));
        Assert.Equal(1, userGrades.GetGradeCount(peerReplacement.Grade));

        var calculator = Scope.ServiceProvider.GetRequiredService<CalculatorService>();
        var expectedWeighted = await calculator.CalculateUserWeightedStats(user, peerReplacement.GameMode);

        Assert.Equal(peerReplacement.TotalScore, userStats.TotalScore);
        Assert.Equal(peerReplacement.MaxCombo, userStats.MaxCombo);
        Assert.Equal(peerReplacement.TotalScore, userStats.RankedScore);
        Assert.Equal(expectedWeighted.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(expectedWeighted.Accuracy, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestDeletionOfBestScorePromotesSameGamemodePeer()
    {
        // Arrange
        var user = await CreateTestUser();
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var replacement = await CreatePersistedScore(user, beatmap, 900, SubmissionStatus.Submitted, "S", 450);
        var score = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Best, "A", 500);

        var handler = new ScoreDeletionHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.Id, filterValidScores: false);
        Assert.NotNull(persistedScore);
        Assert.Equal(SubmissionStatus.Deleted, persistedScore.SubmissionStatus);

        var persistedReplacement = await Database.Scores.GetScore(replacement.Id, filterValidScores: false);
        Assert.NotNull(persistedReplacement);
        Assert.Equal(SubmissionStatus.Best, persistedReplacement.SubmissionStatus);
    }

    [Fact]
    public async Task TestDeletionOfBestScoreDoesNotPromoteScoreInDifferentGamemode()
    {
        // Arrange
        var user = await CreateTestUser();
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var standardScore = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Best, "S", 500);
        var relaxScore = await CreatePersistedScore(user, beatmap, 800, SubmissionStatus.Submitted, "A", 400, Mods.Relax);

        var handler = new ScoreDeletionHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = standardScore.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedStandard = await Database.Scores.GetScore(standardScore.Id, filterValidScores: false);
        Assert.NotNull(persistedStandard);
        Assert.Equal(SubmissionStatus.Deleted, persistedStandard.SubmissionStatus);

        var persistedRelax = await Database.Scores.GetScore(relaxScore.Id, filterValidScores: false);
        Assert.NotNull(persistedRelax);
        Assert.Equal(SubmissionStatus.Submitted, persistedRelax.SubmissionStatus);
    }

    [Fact]
    public async Task TestDeletionOfBestScoreWithMultipleSameGamemodePeersPromotesHighestScore()
    {
        // Arrange
        var user = await CreateTestUser();
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var lowScore = await CreatePersistedScore(user, beatmap, 500, SubmissionStatus.Submitted, "B", 200);
        var midScore = await CreatePersistedScore(user, beatmap, 800, SubmissionStatus.Submitted, "A", 400);
        var bestScore = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Best, "S", 500);

        var handler = new ScoreDeletionHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = bestScore.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedBest = await Database.Scores.GetScore(bestScore.Id, filterValidScores: false);
        Assert.NotNull(persistedBest);
        Assert.Equal(SubmissionStatus.Deleted, persistedBest.SubmissionStatus);

        var persistedMid = await Database.Scores.GetScore(midScore.Id, filterValidScores: false);
        Assert.NotNull(persistedMid);
        Assert.Equal(SubmissionStatus.Best, persistedMid.SubmissionStatus);

        var persistedLow = await Database.Scores.GetScore(lowScore.Id, filterValidScores: false);
        Assert.NotNull(persistedLow);
        Assert.Equal(SubmissionStatus.Submitted, persistedLow.SubmissionStatus);
    }

    [Fact]
    public async Task TestDeletionOfNonBestScoreDoesNotPromoteAnyPeer()
    {
        // Arrange
        var user = await CreateTestUser();
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var possibleBestScore = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Submitted, "S", 500);
        var submittedScore = await CreatePersistedScore(user, beatmap, 800, SubmissionStatus.Submitted, "A", 400);

        var handler = new ScoreDeletionHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = submittedScore.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedPossibleBest = await Database.Scores.GetScore(possibleBestScore.Id, filterValidScores: false);
        Assert.NotNull(persistedPossibleBest);
        Assert.Equal(SubmissionStatus.Submitted, persistedPossibleBest.SubmissionStatus);

        var persistedSubmitted = await Database.Scores.GetScore(submittedScore.Id, filterValidScores: false);
        Assert.NotNull(persistedSubmitted);
        Assert.Equal(SubmissionStatus.Deleted, persistedSubmitted.SubmissionStatus);
    }

    [Fact]
    public async Task TestDeletionOfOnlyScoreOnBeatmap()
    {
        // Arrange
        var user = await CreateTestUser();
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRandomBeatmapWithSet();
        beatmapSet.IgnoreBeatmapRanking();

        var score = await CreatePersistedScore(user, beatmap, 1000, SubmissionStatus.Best, "S", 500);

        var handler = new ScoreDeletionHandler(Database, CreatePipeline());

        // Act
        var result = await handler.ExecuteAsync(new ScoreTaskQueue
            {
                TaskType = ScoreTaskType.Delete,
                ScoreId = score.Id
            },
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetScore(score.Id, filterValidScores: false);
        Assert.NotNull(persistedScore);
        Assert.Equal(SubmissionStatus.Deleted, persistedScore.SubmissionStatus);
    }

    private ScoreCommitPipeline CreatePipeline()
    {
        var database = Scope.ServiceProvider.GetRequiredService<DatabaseService>();

        return new ScoreCommitPipeline(database,
        [
            new LeaderboardProcessor(database),
            new UserGradesScoreProcessor(database),
            new UserStatsScoreProcessor(database, Scope.ServiceProvider.GetRequiredService<CalculatorService>())
        ]);
    }

    private async Task<Score> CreatePersistedScore(
        User user,
        Beatmap beatmap,
        long totalScore,
        SubmissionStatus submissionStatus,
        string grade,
        int maxCombo,
        Mods mods = Mods.None)
    {
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.TotalScore = totalScore;
        score.Grade = grade;
        score.MaxCombo = maxCombo;
        score.Mods = mods;
        score.EnrichWithBeatmapData(beatmap);
        score.GameMode = score.GameMode.EnrichWithMods(score.Mods);
        score.SubmissionStatus = submissionStatus;
        score.LocalProperties = score.LocalProperties.FromScore(score);

        return await CreateTestScore(score);
    }
}