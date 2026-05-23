using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Calculators;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils.Processing;
using Xunit;
using Mods = osu.Shared.Mods;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Processing.Tests.Scores.Processors;

[Collection("Integration tests collection")]
public class UserStatsScoreProcessorTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task TestOnNewSubmissionWithFirstRankedScoreUpdatesStatsAndWeightedValues()
    {
        // Arrange
        var processor = CreateProcessor();
        var calculator = GetCalculator();
        var user = await CreateTestUser();
        var score = CreateScore(user.Id, totalScore: 1000, performancePoints: 100, maxCombo: 400);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var previousStats = userStats.Clone();
        var expectedWeighted = await calculator.CalculateUserWeightedStats(user, score.GameMode, score);

        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        AssertIncrementedCoreStats(previousStats, userStats, score);
        Assert.Equal(previousStats.RankedScore + score.TotalScore, userStats.RankedScore);
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);
        Assert.Equal(expectedWeighted.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(expectedWeighted.Accuracy, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithBetterRankedScoreUpdatesRankedScoreAndWeightedValues()
    {
        // Arrange
        var processor = CreateProcessor();
        var calculator = GetCalculator();
        var user = await CreateTestUser();
        var oldScore = await CreatePersistedScore(user.Id, 1000, 90, 300);
        var score = CreateScore(user.Id, totalScore: 1200, performancePoints: 100, maxCombo: 400);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        await SeedUserStatsFromSingleScore(user, userStats, oldScore);
        var previousStats = userStats.Clone();
        var expectedWeighted = await calculator.CalculateUserWeightedStats(user, score.GameMode, score);

        var context = ScoreCommitContextFactory.Create(
            ScoreTaskType.Submission,
            score,
            user,
            userStats,
            userGrades,
            userPersonalBestScores: new UserBeatmapPeers(null, new UserPersonalBestScores(oldScore)),
            originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        AssertIncrementedCoreStats(previousStats, userStats, score);
        Assert.Equal(previousStats.RankedScore + (score.TotalScore - oldScore.TotalScore), userStats.RankedScore);
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);
        Assert.Equal(expectedWeighted.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(expectedWeighted.Accuracy, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithWorseRankedScoreKeepsRankedAndWeightedValues()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var oldScore = await CreatePersistedScore(user.Id, 1000, 100, 350);
        var score = CreateScore(user.Id, totalScore: 900, performancePoints: 90, maxCombo: 340);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        await SeedUserStatsFromSingleScore(user, userStats, oldScore);
        var previousStats = userStats.Clone();

        var context = ScoreCommitContextFactory.Create(
            ScoreTaskType.Submission,
            score,
            user,
            userStats,
            userGrades,
            userPersonalBestScores: new UserBeatmapPeers(null, new UserPersonalBestScores(oldScore)),
            originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        AssertIncrementedCoreStats(previousStats, userStats, score);
        Assert.Equal(previousStats.RankedScore, userStats.RankedScore);
        Assert.Equal(previousStats.MaxCombo, userStats.MaxCombo);
        Assert.Equal(previousStats.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(previousStats.Accuracy, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithNewAlgorithmBetterTotalOnlyUpdatesRankedScoreOnly()
    {
        // Arrange
        EnvManager.Set("General:UseNewPerformanceCalculationAlgorithm", "true");

        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var oldScore = await CreatePersistedScore(user.Id, 1100, 120, 300);
        var score = CreateScore(user.Id, totalScore: 1200, performancePoints: 100, maxCombo: 400);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        await SeedUserStatsFromSingleScore(user, userStats, oldScore);
        var previousStats = userStats.Clone();

        var context = ScoreCommitContextFactory.Create(
            ScoreTaskType.Submission,
            score,
            user,
            userStats,
            userGrades,
            userPersonalBestScores: new UserBeatmapPeers(null, new UserPersonalBestScores(oldScore, oldScore)),
            originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        AssertIncrementedCoreStats(previousStats, userStats, score);
        Assert.Equal(previousStats.RankedScore + (score.TotalScore - oldScore.TotalScore), userStats.RankedScore);
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);
        Assert.Equal(previousStats.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(previousStats.Accuracy, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithNewAlgorithmBetterPerformanceOnlyUpdatesWeightedValues()
    {
        // Arrange
        EnvManager.Set("General:UseNewPerformanceCalculationAlgorithm", "true");

        var processor = CreateProcessor();
        var calculator = GetCalculator();
        var user = await CreateTestUser();
        var oldScore = await CreatePersistedScore(user.Id, 1200, 100, 300);
        var score = CreateScore(user.Id, totalScore: 1100, performancePoints: 120, maxCombo: 400);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        await SeedUserStatsFromSingleScore(user, userStats, oldScore);
        var previousStats = userStats.Clone();
        var expectedWeighted = await calculator.CalculateUserWeightedStats(user, score.GameMode, score);

        var context = ScoreCommitContextFactory.Create(
            ScoreTaskType.Submission,
            score,
            user,
            userStats,
            userGrades,
            userPersonalBestScores: new UserBeatmapPeers(null, new UserPersonalBestScores(oldScore, oldScore)),
            originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        AssertIncrementedCoreStats(previousStats, userStats, score);
        Assert.Equal(previousStats.RankedScore, userStats.RankedScore);
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);
        Assert.Equal(expectedWeighted.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(expectedWeighted.Accuracy, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithUnrankedScoreableBeatmapUpdatesMaxComboOnly()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var score = CreateScore(user.Id, totalScore: 1000, performancePoints: 100, maxCombo: 450, beatmapStatus: BeatmapStatus.Loved, isScoreable: true);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        userStats.MaxCombo = 100;
        userStats.PerformancePoints = 50;
        userStats.Accuracy = 90;
        var previousStats = userStats.Clone();

        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        AssertIncrementedCoreStats(previousStats, userStats, score);
        Assert.Equal(previousStats.RankedScore, userStats.RankedScore);
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);
        Assert.Equal(previousStats.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(previousStats.Accuracy, userStats.Accuracy, 6);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task TestOnNewSubmissionWithFailedOrUnscoreableScoreKeepsRankedAndWeightedValues(bool isScoreable, bool isPassed)
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var score = CreateScore(user.Id, totalScore: 1000, performancePoints: 100, maxCombo: 450, isScoreable: isScoreable, isPassed: isPassed, beatmapStatus: isScoreable ? BeatmapStatus.Ranked : BeatmapStatus.Pending);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        userStats.MaxCombo = 100;
        userStats.RankedScore = 500;
        userStats.PerformancePoints = 50;
        userStats.Accuracy = 90;
        var previousStats = userStats.Clone();

        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        AssertIncrementedCoreStats(previousStats, userStats, score);
        Assert.Equal(previousStats.RankedScore, userStats.RankedScore);
        Assert.Equal(previousStats.MaxCombo, userStats.MaxCombo);
        Assert.Equal(previousStats.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(previousStats.Accuracy, userStats.Accuracy, 6);
    }

    [Theory]
    [InlineData(GameMode.Standard, 3)]
    [InlineData(GameMode.Taiko, 5)]
    [InlineData(GameMode.CatchTheBeat, 3)]
    [InlineData(GameMode.Mania, 5)]
    public async Task TestOnNewSubmissionWithDifferentGameModesUpdatesExpectedTotalHits(GameMode mode, int expectedDelta)
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var score = CreateScore(user.Id, gameMode: mode, isScoreable: false, beatmapStatus: BeatmapStatus.Pending, count300: 1, count100: 1, count50: 1, countGeki: 1, countKatu: 1);
        var (userStats, userGrades) = await LoadUserState(user, mode);
        var previousStats = userStats.Clone();

        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Submission, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.Equal(previousStats.TotalHits + expectedDelta, userStats.TotalHits);
    }

    [Fact]
    public async Task TestOnDeletionWithBestRankedScoreUpdatesFallbackMaxComboRankedScoreAndWeightedValues()
    {
        // Arrange
        var processor = CreateProcessor();
        var calculator = GetCalculator();
        var user = await CreateTestUser();
        var promotedPeer = await CreatePersistedScore(user.Id, 900, 90, 450);
        var score = CreateScore(user.Id, 1234, 1000, 100, 500, submissionStatus: SubmissionStatus.Best);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);

        userStats.TotalScore = score.TotalScore + promotedPeer.TotalScore;
        userStats.TotalHits = GetTotalHitsDelta(score) + GetTotalHitsDelta(promotedPeer);
        userStats.PlayTime = score.TimeElapsed + promotedPeer.TimeElapsed;
        userStats.PlayCount = 2;
        userStats.RankedScore = score.TotalScore;
        userStats.MaxCombo = score.MaxCombo;
        userStats.PerformancePoints = 999;
        userStats.Accuracy = 88;
        var expectedWeighted = await calculator.CalculateUserWeightedStats(user, score.GameMode);
        var previousStats = userStats.Clone();

        var context = ScoreCommitContextFactory.Create(
            ScoreTaskType.Delete,
            score,
            user,
            userStats,
            userGrades,
            userPersonalBestScores: new UserBeatmapPeers(new UserPersonalBestScores(promotedPeer), new UserPersonalBestScores(promotedPeer)),
            originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnDeletion(context);

        // Assert
        Assert.Equal(Math.Max(0, previousStats.TotalScore - score.TotalScore), userStats.TotalScore);
        Assert.Equal(Math.Max(0, previousStats.TotalHits - GetTotalHitsDelta(score)), userStats.TotalHits);
        Assert.Equal(Math.Max(0, previousStats.PlayTime - score.TimeElapsed), userStats.PlayTime);
        Assert.Equal(Math.Max(0, previousStats.PlayCount - 1), userStats.PlayCount);
        Assert.Equal(previousStats.RankedScore - (score.TotalScore - promotedPeer.TotalScore), userStats.RankedScore);
        Assert.Equal(450, userStats.MaxCombo);
        Assert.Equal(expectedWeighted.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(expectedWeighted.Accuracy, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestOnDeletionWithFailedOriginalKeepsRankedAndWeightedValues()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var score = CreateScore(user.Id, 1234, 1000, 100, 500, submissionStatus: SubmissionStatus.Failed, isPassed: false);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);

        userStats.TotalScore = score.TotalScore;
        userStats.TotalHits = GetTotalHitsDelta(score);
        userStats.PlayTime = score.TimeElapsed;
        userStats.PlayCount = 1;
        userStats.RankedScore = 500;
        userStats.MaxCombo = 200;
        userStats.PerformancePoints = 75;
        userStats.Accuracy = 91;
        var previousStats = userStats.Clone();

        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Delete, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnDeletion(context);

        // Assert
        Assert.Equal(0, userStats.TotalScore);
        Assert.Equal(0, userStats.TotalHits);
        Assert.Equal(0, userStats.PlayTime);
        Assert.Equal(0, userStats.PlayCount);
        Assert.Equal(previousStats.RankedScore, userStats.RankedScore);
        Assert.Equal(previousStats.MaxCombo, userStats.MaxCombo);
        Assert.Equal(previousStats.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(previousStats.Accuracy, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestOnRecalculationWithRankedPassedScoreRefreshesWeightedValues()
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var score = await CreatePersistedScore(user.Id, 1000, 100, 400);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);

        score.PerformancePoints = 140;
        score.Accuracy = 98;
        await Database.Scores.UpdateScore(score);

        var (expectedWeightedPerformancePoints, expectedWeightedAccuracy) = (PerformanceCalculator.CalculateUserWeightedPerformance([score]), PerformanceCalculator.CalculateUserWeightedAccuracy([score]));

        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Recalculation, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnRecalculation(context);

        // Assert
        var (updatedUserStats, _) = await LoadUserState(user, score.GameMode);

        Assert.Equal(expectedWeightedPerformancePoints, updatedUserStats.PerformancePoints, 6);
        Assert.Equal(expectedWeightedAccuracy, updatedUserStats.Accuracy, 6);
    }

    [Theory]
    [InlineData(false, true, BeatmapStatus.Ranked)]
    [InlineData(true, false, BeatmapStatus.Ranked)]
    [InlineData(true, true, BeatmapStatus.Loved)]
    public async Task TestOnRecalculationWithNonRankedOrFailedScoreKeepsWeightedValues(bool isScoreable, bool isPassed, BeatmapStatus beatmapStatus)
    {
        // Arrange
        var processor = CreateProcessor();
        var user = await CreateTestUser();
        var score = CreateScore(user.Id, isScoreable: isScoreable, isPassed: isPassed, beatmapStatus: beatmapStatus);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        userStats.PerformancePoints = 50;
        userStats.Accuracy = 90;

        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Recalculation, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnRecalculation(context);

        // Assert
        Assert.Equal(50, userStats.PerformancePoints, 6);
        Assert.Equal(90, userStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestOnRestorationWithRankedScoreUpdatesStatsAndWeightedValues()
    {
        // Arrange
        var processor = CreateProcessor();
        var calculator = GetCalculator();
        var user = await CreateTestUser();
        var score = CreateScore(user.Id, totalScore: 1000, performancePoints: 100, maxCombo: 400);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var previousStats = userStats.Clone();
        var expectedWeighted = await calculator.CalculateUserWeightedStats(user, score.GameMode, score);

        var context = ScoreCommitContextFactory.Create(ScoreTaskType.Restore, score, user, userStats, userGrades, originalState: ScoreStateSnapshot.Capture(score));

        // Act
        await processor.OnRestoration(context);

        // Assert
        AssertIncrementedCoreStats(previousStats, userStats, score);
        Assert.Equal(previousStats.RankedScore + score.TotalScore, userStats.RankedScore);
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);
        Assert.Equal(expectedWeighted.PerformancePoints, userStats.PerformancePoints, 6);
        Assert.Equal(expectedWeighted.Accuracy, userStats.Accuracy, 6);
    }

    private CalculatorService GetCalculator()
    {
        return Scope.ServiceProvider.GetRequiredService<CalculatorService>();
    }

    private UserStatsScoreProcessor CreateProcessor()
    {
        return new UserStatsScoreProcessor(Database, GetCalculator());
    }

    private async Task<(UserStats UserStats, UserGrades UserGrades)> LoadUserState(User user, GameMode mode)
    {
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, mode);
        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, mode);

        Assert.NotNull(userStats);
        Assert.NotNull(userGrades);

        return (userStats, userGrades);
    }

    private async Task<Score> CreatePersistedScore(
        int userId,
        long totalScore,
        double performancePoints,
        int maxCombo,
        SubmissionStatus submissionStatus = SubmissionStatus.Best,
        bool isPassed = true,
        GameMode gameMode = GameMode.Standard,
        Mods mods = Mods.None)
    {
        var score = CreateScore(userId, totalScore: totalScore, performancePoints: performancePoints, maxCombo: maxCombo, submissionStatus: submissionStatus, isPassed: isPassed, gameMode: gameMode, mods: mods);
        return await CreateTestScore(score);
    }

    private async Task SeedUserStatsFromSingleScore(User user, UserStats userStats, Score score)
    {
        userStats.TotalScore = score.TotalScore;
        userStats.TotalHits = GetTotalHitsDelta(score);
        userStats.PlayTime = score.TimeElapsed;
        userStats.PlayCount = 1;
        userStats.RankedScore = score.LocalProperties.IsRanked ? score.TotalScore : 0;
        userStats.MaxCombo = score.IsScoreable && score.IsPassed ? score.MaxCombo : 0;

        var weighted = await GetCalculator().CalculateUserWeightedStats(user, score.GameMode);
        userStats.PerformancePoints = weighted.PerformancePoints;
        userStats.Accuracy = weighted.Accuracy;
    }

    private static void AssertIncrementedCoreStats(UserStats previousStats, UserStats currentStats, Score score)
    {
        Assert.Equal(previousStats.TotalScore + score.TotalScore, currentStats.TotalScore);
        Assert.Equal(previousStats.TotalHits + GetTotalHitsDelta(score), currentStats.TotalHits);
        Assert.Equal(previousStats.PlayTime + score.TimeElapsed, currentStats.PlayTime);
        Assert.Equal(previousStats.PlayCount + 1, currentStats.PlayCount);
    }

    private static int GetTotalHitsDelta(Score score)
    {
        var delta = score.Count300 + score.Count100 + score.Count50;

        if ((GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania)
            delta += score.CountGeki + score.CountKatu;

        return delta;
    }

    private static Score CreateScore(
        int userId,
        int id = 0,
        long totalScore = 1000,
        double performancePoints = 100,
        int maxCombo = 400,
        bool isPassed = true,
        bool isScoreable = true,
        GameMode gameMode = GameMode.Standard,
        Mods mods = Mods.None,
        SubmissionStatus submissionStatus = SubmissionStatus.Best,
        BeatmapStatus beatmapStatus = BeatmapStatus.Ranked,
        int count300 = 100,
        int count100 = 10,
        int count50 = 0,
        int countGeki = 0,
        int countKatu = 0)
    {
        var score = new Score
        {
            Id = id,
            UserId = userId,
            BeatmapId = 11,
            BeatmapHash = "user-stats-beatmap-hash",
            ScoreHash = $"{Guid.NewGuid():N}",
            TotalScore = totalScore,
            MaxCombo = maxCombo,
            Count300 = count300,
            Count100 = count100,
            Count50 = count50,
            CountMiss = isPassed ? 0 : 1,
            CountKatu = countKatu,
            CountGeki = countGeki,
            Perfect = false,
            Mods = mods,
            Grade = isPassed ? "A" : "F",
            IsPassed = isPassed,
            IsScoreable = isScoreable,
            SubmissionStatus = submissionStatus,
            GameMode = gameMode,
            WhenPlayed = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            OsuVersion = "b20260101.1",
            BeatmapStatus = beatmapStatus,
            ClientTime = new DateTime(2026, 1, 2, 3, 4, 5),
            Accuracy = isPassed ? 98 : 50,
            PerformancePoints = performancePoints,
            TimeElapsed = 120
        };

        score.LocalProperties = score.LocalProperties.FromScore(score);
        return score;
    }
}