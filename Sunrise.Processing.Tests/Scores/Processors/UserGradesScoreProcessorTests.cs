using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils.Processing;
using Xunit;
using Mods = osu.Shared.Mods;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Processing.Tests.Scores.Processors;

[Collection("Integration tests collection")]
public class UserGradesScoreProcessorTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestOnNewSubmissionWithBestScoreIncrementsMatchingGradeCount()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard
        };
        var score = CreateScore(user);
        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.Equal(1, userGrades.CountA);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithPreviousBestReplacesGradeCounts()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard,
            CountS = 1
        };

        var previousBest = CreateScore(user, "S", submissionStatus: SubmissionStatus.Best);
        var score = CreateScore(user);

        var context = ScoreCommitContextFactory.Create(
            ScoreTaskType.Submission,
            score,
            user,
            userStats,
            userGrades,
            userPersonalBestScores: new UserBeatmapPeers(null, new UserPersonalBestScores(previousBest)),
            originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.Equal(0, userGrades.CountS);
        Assert.Equal(1, userGrades.CountA);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithModSpecificBestButWorseOverallKeepsGradesUnchanged()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard,
            CountS = 1
        };

        var existingOverallBest = CreateScore(user, "S", submissionStatus: SubmissionStatus.Best);
        existingOverallBest.TotalScore = 1200;

        var score = CreateScore(user);
        score.TotalScore = 1100;

        var context = ScoreCommitContextFactory.Create(
            ScoreTaskType.Submission,
            score,
            user,
            userStats,
            userGrades,
            userPersonalBestScores: new UserBeatmapPeers(null, new UserPersonalBestScores(existingOverallBest)),
            originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.Equal(1, userGrades.CountS);
        Assert.Equal(0, userGrades.CountA);
    }

    [Theory]
    [InlineData(false, true, SubmissionStatus.Best)]
    [InlineData(true, false, SubmissionStatus.Best)]
    [InlineData(true, true, SubmissionStatus.Submitted)]
    public async Task TestOnNewSubmissionWithInvalidScoreStateKeepsGradesUnchanged(bool isScoreable, bool isPassed, SubmissionStatus submissionStatus)
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard,
            CountA = 2
        };
        var score = CreateScore(user, isScoreable: isScoreable, isPassed: isPassed, submissionStatus: submissionStatus);
        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.Equal(2, userGrades.CountA);
    }

    [Fact]
    public async Task TestOnRecalculationReturnsWithoutChangingGrades()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard,
            CountA = 2
        };
        var score = CreateScore(user);
        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Recalculation, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnRecalculation(context);

        // Assert
        Assert.Equal(2, userGrades.CountA);
    }

    [Fact]
    public async Task TestOnDeletionWithBestOriginalStateDecrementsMatchingGradeCount()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard,
            CountA = 1
        };
        var score = CreateScore(user);
        var originalState = ScoreStateSnapshot.Capture(score);
        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Delete, score, user, userStats, userGrades, originalState: originalState);

        // Act
        await processor.OnDeletion(context);

        // Assert
        Assert.Equal(0, userGrades.CountA);
    }

    [Fact]
    public async Task TestOnDeletionWithPromotedReplacementReplacesGradeCounts()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard,
            CountA = 1
        };

        var promotedReplacement = CreateScore(user, "S", submissionStatus: SubmissionStatus.Best);
        var score = CreateScore(user);
        var originalState = ScoreStateSnapshot.Capture(score);

        var context = ScoreCommitContextFactory.Create(
            ScoreTaskType.Delete,
            score,
            user,
            userStats,
            userGrades,
            userPersonalBestScores: new UserBeatmapPeers(new UserPersonalBestScores(promotedReplacement), new UserPersonalBestScores(promotedReplacement)),
            originalState: originalState);

        // Act
        await processor.OnDeletion(context);

        // Assert
        Assert.Equal(0, userGrades.CountA);
        Assert.Equal(1, userGrades.CountS);
    }

    [Fact]
    public async Task TestOnDeletionWithPromotedReplacementWithSameGradeNoChangesRequired()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard,
            CountA = 1
        };

        var promotedReplacement = CreateScore(user, submissionStatus: SubmissionStatus.Best);
        var score = CreateScore(user);
        var originalState = ScoreStateSnapshot.Capture(score);

        var context = ScoreCommitContextFactory.Create(
            ScoreTaskType.Delete,
            score,
            user,
            userStats,
            userGrades,
            userPersonalBestScores: new UserBeatmapPeers(new UserPersonalBestScores(promotedReplacement), new UserPersonalBestScores(promotedReplacement)),
            originalState: originalState);

        // Act
        await processor.OnDeletion(context);

        // Assert
        Assert.Equal(1, userGrades.CountA);
    }

    [Fact]
    public async Task TestOnDeletionWithNonBestOriginalStateKeepsGradesUnchanged()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard,
            CountA = 1
        };
        var score = CreateScore(user, submissionStatus: SubmissionStatus.Submitted);
        var originalState = ScoreStateSnapshot.Capture(score);
        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Delete, score, user, userStats, userGrades, originalState: originalState);

        // Act
        await processor.OnDeletion(context);

        // Assert
        Assert.Equal(1, userGrades.CountA);
    }

    [Fact]
    public async Task TestOnRestorationWithBestScoreIncrementsMatchingGradeCount()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard
        };
        var score = CreateScore(user);
        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Restore, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnRestoration(context);

        // Assert
        Assert.Equal(1, userGrades.CountA);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithUnknownGradeThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var processor = new UserGradesScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);
        var userGrades = new UserGrades
        {
            UserId = user.Id,
            GameMode = GameMode.Standard
        };
        var score = CreateScore(user, "Z", submissionStatus: SubmissionStatus.Best);
        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => processor.OnNewSubmission(context));
    }

    // TODO: Refactor this to proper fixture
    private Score CreateScore(
        User user,
        string grade = "A",
        bool isScoreable = true,
        bool isPassed = true,
        SubmissionStatus submissionStatus = SubmissionStatus.Best)
    {
        var beatmap = _mocker.Beatmap.GetRandomBeatmap();
        beatmap.StatusString = isScoreable ? "ranked" : "pending";
        beatmap.ModeInt = (int)GameMode.Standard;

        var score = new Score
        {
            ScoreHash = $"{Guid.NewGuid():N}",
            TotalScore = 1000,
            MaxCombo = 100,
            Count300 = 100,
            Count100 = 10,
            Count50 = 0,
            CountMiss = isPassed ? 0 : 1,
            CountKatu = 0,
            CountGeki = 0,
            Perfect = false,
            Mods = Mods.None,
            Grade = grade,
            IsPassed = isPassed,
            IsScoreable = isScoreable,
            SubmissionStatus = submissionStatus,
            WhenPlayed = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            OsuVersion = "b20260101.1",
            ClientTime = new DateTime(2026, 1, 2, 3, 4, 5),
            Accuracy = isPassed ? 98 : 50,
            PerformancePoints = 100,
            TimeElapsed = 120
        };

        score.EnrichWithUserData(user);
        score.EnrichWithBeatmapData(beatmap);
        score.LocalProperties = score.LocalProperties.FromScore(score);
        return score;
    }
}