using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils.Processing;
using Xunit;
using Mods = osu.Shared.Mods;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Processing.Tests.Scores.Processors;

[Collection("Integration tests collection")]
public class LeaderboardProcessorTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task TestOnNewSubmissionWithBetterScoreReturnsBestAndDemotesPreviousBest()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var previousBest = await CreatePersistedScore(user.Id, 1000, SubmissionStatus.Best);
        var score = CreateScore(user.Id, 1200, SubmissionStatus.Submitted);
        var context = await CreateContext(ScoreTaskType.Submission, score, user, previousBest, ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.Equal(SubmissionStatus.Best, score.SubmissionStatus);

        var persistedPreviousBest = await Database.Scores.GetScore(previousBest.Id, filterValidScores: false);
        Assert.NotNull(persistedPreviousBest);
        Assert.Equal(SubmissionStatus.Submitted, persistedPreviousBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithWorseScoreReturnsSubmittedAndKeepsPreviousBest()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var previousBest = await CreatePersistedScore(user.Id, 1000, SubmissionStatus.Best);
        var score = CreateScore(user.Id, 900, SubmissionStatus.Submitted);
        var context = await CreateContext(ScoreTaskType.Submission, score, user, previousBest, ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.Equal(SubmissionStatus.Submitted, score.SubmissionStatus);

        var persistedPreviousBest = await Database.Scores.GetScore(previousBest.Id, filterValidScores: false);
        Assert.NotNull(persistedPreviousBest);
        Assert.Equal(SubmissionStatus.Best, persistedPreviousBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestOnRecalculationWithBetterScoreReturnsBestAndDemotesPreviousBest()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var previousBest = await CreatePersistedScore(user.Id, 1000, SubmissionStatus.Best);
        var score = CreateScore(user.Id, 1200, SubmissionStatus.Submitted);
        var context = await CreateContext(ScoreTaskType.Recalculation, score, user, previousBest, ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnRecalculation(context);

        // Assert
        Assert.Equal(SubmissionStatus.Best, score.SubmissionStatus);

        var persistedPreviousBest = await Database.Scores.GetScore(previousBest.Id, filterValidScores: false);
        Assert.NotNull(persistedPreviousBest);
        Assert.Equal(SubmissionStatus.Submitted, persistedPreviousBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestOnRecalculationWithWorseScoreReturnsSubmittedAndKeepsPreviousBest()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var previousBest = await CreatePersistedScore(user.Id, 1000, SubmissionStatus.Best);
        var score = CreateScore(user.Id, 900, SubmissionStatus.Submitted);
        var context = await CreateContext(ScoreTaskType.Recalculation, score, user, previousBest, ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnRecalculation(context);

        // Assert
        Assert.Equal(SubmissionStatus.Submitted, score.SubmissionStatus);

        var persistedPreviousBest = await Database.Scores.GetScore(previousBest.Id, filterValidScores: false);
        Assert.NotNull(persistedPreviousBest);
        Assert.Equal(SubmissionStatus.Best, persistedPreviousBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestOnDeletionWithBestOriginalStatePromotesNextBestPeer()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var nextBest = await CreatePersistedScore(user.Id, 1000, SubmissionStatus.Submitted);
        var score = CreateScore(user.Id, 1200, SubmissionStatus.Best);
        var originalState = ScoreStateSnapshot.Capture(score);
        var context = await CreateContext(ScoreTaskType.Delete, score, user, nextBest, originalState);

        // Act
        await processor.OnDeletion(context);

        // Assert
        Assert.Equal(SubmissionStatus.Deleted, score.SubmissionStatus);

        var persistedNextBest = await Database.Scores.GetScore(nextBest.Id, filterValidScores: false);
        Assert.NotNull(persistedNextBest);
        Assert.Equal(SubmissionStatus.Best, persistedNextBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestOnDeletionWithSubmittedOriginalStateKeepsPeerUnchanged()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var nextBest = await CreatePersistedScore(user.Id, 1000, SubmissionStatus.Submitted);
        var score = CreateScore(user.Id, 900, SubmissionStatus.Submitted);
        var originalState = ScoreStateSnapshot.Capture(score);
        var context = await CreateContext(ScoreTaskType.Delete, score, user, nextBest, originalState);

        // Act
        await processor.OnDeletion(context);

        // Assert
        Assert.Equal(SubmissionStatus.Deleted, score.SubmissionStatus);

        var persistedNextBest = await Database.Scores.GetScore(nextBest.Id, filterValidScores: false);
        Assert.NotNull(persistedNextBest);
        Assert.Equal(SubmissionStatus.Submitted, persistedNextBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestOnRestorationWithPassedBetterScoreReturnsBestAndDemotesPreviousBest()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var previousBest = await CreatePersistedScore(user.Id, 1000, SubmissionStatus.Best);
        var score = CreateScore(user.Id, 1200, SubmissionStatus.Deleted);
        var originalState = ScoreStateSnapshot.Capture(score);
        var context = await CreateContext(ScoreTaskType.Restore, score, user, previousBest, originalState);

        // Act
        await processor.OnRestoration(context);

        // Assert
        Assert.Equal(SubmissionStatus.Best, score.SubmissionStatus);

        var persistedPreviousBest = await Database.Scores.GetScore(previousBest.Id, filterValidScores: false);
        Assert.NotNull(persistedPreviousBest);
        Assert.Equal(SubmissionStatus.Submitted, persistedPreviousBest.SubmissionStatus);
    }

    [Fact]
    public async Task TestOnRestorationWithFailedScoreReturnsFailedAndKeepsPreviousBest()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var previousBest = await CreatePersistedScore(user.Id, 1000, SubmissionStatus.Best);
        var score = CreateScore(user.Id, 1200, SubmissionStatus.Deleted, false);
        var originalState = ScoreStateSnapshot.Capture(score);
        var context = await CreateContext(ScoreTaskType.Restore, score, user, previousBest, originalState);

        // Act
        await processor.OnRestoration(context);

        // Assert
        Assert.Equal(SubmissionStatus.Failed, score.SubmissionStatus);

        var persistedPreviousBest = await Database.Scores.GetScore(previousBest.Id, filterValidScores: false);
        Assert.NotNull(persistedPreviousBest);
        Assert.Equal(SubmissionStatus.Best, persistedPreviousBest.SubmissionStatus);
    }

    private async Task<ScoreCommitContext> CreateContext(
        ScoreTaskType taskType,
        Score score,
        User user,
        Score? sameModsPeer,
        ScoreStateSnapshot originalState)
    {
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, score.GameMode);

        Assert.NotNull(userStats);
        Assert.NotNull(userGrades);

        var peers = sameModsPeer == null
            ? null
            : new UserBeatmapPeers(new UserPersonalBestScores(sameModsPeer), new UserPersonalBestScores(sameModsPeer));

        return ScoreCommitContextFactory.Create(taskType, score, user, userStats, userGrades, userPersonalBestScores: peers, originalState: originalState);
    }

    private async Task<Score> CreatePersistedScore(int userId, long totalScore, SubmissionStatus submissionStatus, bool isPassed = true)
    {
        var score = CreateScore(userId, totalScore, submissionStatus, isPassed);
        return await CreateTestScore(score);
    }

    private static Score CreateScore(int userId, long totalScore, SubmissionStatus submissionStatus, bool isPassed = true)
    {
        var score = new Score
        {
            UserId = userId,
            BeatmapId = 11,
            BeatmapHash = "leaderboard-beatmap-hash",
            ScoreHash = $"{Guid.NewGuid():N}",
            TotalScore = totalScore,
            MaxCombo = 100,
            Count300 = 100,
            Count100 = 10,
            Count50 = 0,
            CountMiss = isPassed ? 0 : 1,
            CountKatu = 0,
            CountGeki = 0,
            Perfect = false,
            Mods = Mods.None,
            Grade = isPassed ? "A" : "F",
            IsPassed = isPassed,
            IsScoreable = true,
            SubmissionStatus = submissionStatus,
            GameMode = GameMode.Standard,
            WhenPlayed = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            OsuVersion = "b20260101.1",
            BeatmapStatus = BeatmapStatus.Ranked,
            ClientTime = new DateTime(2026, 1, 2, 3, 4, 5),
            Accuracy = isPassed ? 98 : 50,
            PerformancePoints = totalScore,
            TimeElapsed = 120
        };

        score.LocalProperties = score.LocalProperties.FromScore(score);
        return score;
    }

    private LeaderboardProcessor CreateProcessor()
    {
        return new LeaderboardProcessor(Database);
    }
}