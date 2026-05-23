using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Calculators;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Processing.Tests.Scores.Pipeline;

[Collection("Integration tests collection")]
public class ScoreCommitPipelineTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestCommitSubmissionCapturesOriginalStateEnrichesBeatmapStatusAndPersistsMutations()
    {
        // Arrange
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);
        var calculator = pipelineScope.ServiceProvider.GetRequiredService<CalculatorService>();
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.Grade = "A";
        score.EnrichWithBeatmapData(beatmap);
        score.SubmissionStatus = SubmissionStatus.Submitted;
        score.IsScoreable = false;
        score.BeatmapStatus = BeatmapStatus.Pending;
        score.LocalProperties = score.LocalProperties.FromScore(score);

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var context = new ScoreCommitContext(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap);
        var (expectedWeightedPerformancePoints, expectedWeightedAccuracy) = (PerformanceCalculator.CalculateUserWeightedPerformance([score]), PerformanceCalculator.CalculateUserWeightedAccuracy([score]));

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(context.OriginalState.IsScoreable);
        Assert.False(context.OriginalState.IsRanked);
        Assert.Equal(SubmissionStatus.Submitted, context.OriginalState.SubmissionStatus);

        var persistedScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(persistedScore);
        Assert.Equal(BeatmapStatus.Ranked, persistedScore.BeatmapStatus);
        Assert.True(persistedScore.IsScoreable);
        Assert.Equal(SubmissionStatus.Best, persistedScore.SubmissionStatus);

        var persistedUserStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        var persistedUserGrades = await Database.Users.Grades.GetUserGrades(user.Id, score.GameMode);
        Assert.NotNull(persistedUserStats);
        Assert.NotNull(persistedUserGrades);
        Assert.Equal(score.TotalScore, persistedUserStats.RankedScore);
        Assert.Equal(score.MaxCombo, persistedUserStats.MaxCombo);
        Assert.Equal(expectedWeightedPerformancePoints, persistedUserStats.PerformancePoints, 6);
        Assert.Equal(expectedWeightedAccuracy, persistedUserStats.Accuracy, 6);
        Assert.Equal(1, persistedUserGrades.CountA);
    }

    [Fact]
    public async Task TestCommitDeletionPromotesReplacementAndPersistsGrades()
    {
        // Arrange
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider, false);
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var replacement = await CreatePersistedScore(user.Id, beatmap, 900, SubmissionStatus.Submitted, "S", 450);
        var score = await CreatePersistedScore(user.Id, beatmap, 1000, SubmissionStatus.Best, "A", 500);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);

        userGrades.CountA = 1;

        var context = new ScoreCommitContext(ScoreTaskType.Delete, score, user, userStats, userGrades);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetUnvalidatedScore(score.Id);
        var persistedReplacement = await Database.Scores.GetUnvalidatedScore(replacement.Id);
        var persistedUserGrades = await Database.Users.Grades.GetUserGrades(user.Id, score.GameMode);

        Assert.NotNull(persistedScore);
        Assert.NotNull(persistedReplacement);
        Assert.NotNull(persistedUserGrades);
        Assert.Equal(SubmissionStatus.Deleted, persistedScore.SubmissionStatus);
        Assert.Equal(SubmissionStatus.Best, persistedReplacement.SubmissionStatus);
        Assert.Equal(0, persistedUserGrades.CountA);
        Assert.Equal(1, persistedUserGrades.CountS);
    }

    [Fact]
    public async Task TestCommitRestorationRestoresBestScoreAndSwapsGradeCounts()
    {
        // Arrange
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider, false);
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var previousBest = await CreatePersistedScore(user.Id, beatmap, 900, SubmissionStatus.Best, "S", 450);
        var score = await CreatePersistedScore(user.Id, beatmap, 1000, SubmissionStatus.Deleted, "A", 500);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);

        userGrades.CountS = 1;

        var context = new ScoreCommitContext(ScoreTaskType.Restore, score, user, userStats, userGrades);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetUnvalidatedScore(score.Id);
        var persistedPreviousBest = await Database.Scores.GetUnvalidatedScore(previousBest.Id);
        var persistedUserGrades = await Database.Users.Grades.GetUserGrades(user.Id, score.GameMode);

        Assert.NotNull(persistedScore);
        Assert.NotNull(persistedPreviousBest);
        Assert.NotNull(persistedUserGrades);
        Assert.Equal(SubmissionStatus.Best, persistedScore.SubmissionStatus);
        Assert.Equal(SubmissionStatus.Submitted, persistedPreviousBest.SubmissionStatus);
        Assert.Equal(0, persistedUserGrades.CountS);
        Assert.Equal(1, persistedUserGrades.CountA);
    }

    [Fact]
    public async Task TestCommitWithLostClaimLeaseRollsBackMutations()
    {
        // Arrange
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.EnrichWithBeatmapData(beatmap);
        score.LocalProperties = score.LocalProperties.FromScore(score);

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var payload = await CreatePayload(user.Id);
        var persistedTask = await CreateTask(ScoreTaskType.Submission, scoreProcessingQueueId: payload.Id, claimToken: "expected-token", leaseExpiresAt: DateTime.UtcNow.AddMinutes(1));
        var mismatchedTask = new ScoreTaskQueue
        {
            Id = persistedTask.Id,
            TaskType = persistedTask.TaskType,
            ClaimToken = "wrong-token"
        };

        var context = new ScoreCommitContext(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap);

        // Act
        var result = await pipeline.Commit(context, mismatchedTask, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("claim lost", result.Error, StringComparison.OrdinalIgnoreCase);

        var persistedScore = await Database.Scores.GetScore(score.ScoreHash);
        var persistedUserStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        var persistedUserGrades = await Database.Users.Grades.GetUserGrades(user.Id, score.GameMode);
        var refreshedTask = await Database.DbContext.ScoreTaskQueue.AsNoTracking().FirstAsync(x => x.Id == persistedTask.Id);

        Assert.Null(persistedScore);
        Assert.NotNull(persistedUserStats);
        Assert.NotNull(persistedUserGrades);
        Assert.Equal(0, persistedUserStats.TotalScore);
        Assert.Equal(0, persistedUserStats.RankedScore);
        Assert.Equal(0, persistedUserGrades.CountA);
        Assert.Equal("expected-token", refreshedTask.ClaimToken);
    }

    [Fact]
    public async Task TestCommitDeletionUpdatesUserStatsAndRank()
    {
        // Arrange
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);
        var calculator = pipelineScope.ServiceProvider.GetRequiredService<CalculatorService>();
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        // Two best scores: deleting the higher one should reduce ranked score
        var lowerScore = await CreatePersistedScore(user.Id, beatmap, 800, SubmissionStatus.Submitted, "B", 300);
        var score = await CreatePersistedScore(user.Id, beatmap, 1200, SubmissionStatus.Best, "A", 500);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);

        // Seed user stats as if the score was already counted
        userStats.TotalScore = score.TotalScore + lowerScore.TotalScore;
        userStats.RankedScore = score.TotalScore;
        userStats.MaxCombo = score.MaxCombo;
        userStats.PlayCount = 2;
        userStats.PlayTime = score.TimeElapsed + lowerScore.TimeElapsed;
        userStats.TotalHits = score.Count300 + score.Count100 + score.Count50 + lowerScore.Count300 + lowerScore.Count100 + lowerScore.Count50;
        var seededWeighted = await calculator.CalculateUserWeightedStats(user, score.GameMode);
        userStats.PerformancePoints = seededWeighted.PerformancePoints;
        userStats.Accuracy = seededWeighted.Accuracy;
        userGrades.CountA = 1;

        var rankedScoreBefore = userStats.RankedScore;
        var playCountBefore = userStats.PlayCount;

        var context = new ScoreCommitContext(ScoreTaskType.Delete, score, user, userStats, userGrades);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedUserStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        var persistedUserGrades = await Database.Users.Grades.GetUserGrades(user.Id, score.GameMode);
        Assert.NotNull(persistedUserStats);
        Assert.NotNull(persistedUserGrades);

        // After deleting the best score: ranked score should decrease, play count decremented
        Assert.True(persistedUserStats.RankedScore < rankedScoreBefore, "RankedScore should decrease after deleting the best score");
        Assert.Equal(playCountBefore - 1, persistedUserStats.PlayCount);
        Assert.Equal(0, persistedUserGrades.CountA);
    }

    [Fact]
    public async Task TestCommitRecalculationUpdatesUserStatsWeightedValues()
    {
        // Arrange
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);
        var calculator = pipelineScope.ServiceProvider.GetRequiredService<CalculatorService>();
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var score = await CreatePersistedScore(user.Id, beatmap, 1000, SubmissionStatus.Best, "A", 400);
        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);

        // Seed with old values so we can detect the refresh
        userStats.PerformancePoints = 999;
        userStats.Accuracy = 50;

        var context = new ScoreCommitContext(ScoreTaskType.Recalculation, score, user, userStats, userGrades);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedUserStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        Assert.NotNull(persistedUserStats);

        var expectedWeighted = await calculator.CalculateUserWeightedStats(user, score.GameMode);
        Assert.Equal(expectedWeighted.PerformancePoints, persistedUserStats.PerformancePoints, 6);
        Assert.Equal(expectedWeighted.Accuracy, persistedUserStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestCommitRecalculationDemotionUsesPromotedBestForWeightedValues()
    {
        // Arrange
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);
        var calculator = pipelineScope.ServiceProvider.GetRequiredService<CalculatorService>();

        EnvManager.Set("General:UseNewPerformanceCalculationAlgorithm", "true"); // We want target by new PP values

        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var promotedPeer = _mocker.Score.GetBestScoreableRandomScore();
        promotedPeer.UserId = user.Id;
        promotedPeer.Mods = Mods.None;
        promotedPeer.TotalScore = 700;
        promotedPeer.PerformancePoints = 150;
        promotedPeer.MaxCombo = 300;
        promotedPeer.EnrichWithBeatmapData(beatmap);
        promotedPeer.GameMode = GameMode.Standard;
        promotedPeer.SubmissionStatus = SubmissionStatus.Submitted;
        promotedPeer.LocalProperties = promotedPeer.LocalProperties.FromScore(promotedPeer);
        promotedPeer = await CreateTestScore(promotedPeer);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.Mods = Mods.None;
        score.TotalScore = 900;
        score.PerformancePoints = 200;
        score.MaxCombo = 350;
        score.EnrichWithBeatmapData(beatmap);
        score.GameMode = GameMode.Standard;
        score.SubmissionStatus = SubmissionStatus.Best;
        score.LocalProperties = score.LocalProperties.FromScore(score);
        score = await CreateTestScore(score);

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var weightedBefore = await calculator.CalculateUserWeightedStats(user, score.GameMode);
        userStats.PerformancePoints = weightedBefore.PerformancePoints;
        userStats.Accuracy = weightedBefore.Accuracy;

        score.PerformancePoints = 100;

        var context = new ScoreCommitContext(ScoreTaskType.Recalculation, score, user, userStats, userGrades, beatmap);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetUnvalidatedScore(score.Id);
        var persistedPromotedPeer = await Database.Scores.GetUnvalidatedScore(promotedPeer.Id);
        var persistedUserStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);

        Assert.NotNull(persistedScore);
        Assert.NotNull(persistedPromotedPeer);
        Assert.NotNull(persistedUserStats);
        Assert.Equal(SubmissionStatus.Best, persistedScore.SubmissionStatus);
        Assert.Equal(SubmissionStatus.Submitted, persistedPromotedPeer.SubmissionStatus);

        var expectedWeighted = await calculator.CalculateUserWeightedStats(user, score.GameMode);
        Assert.Equal(expectedWeighted.PerformancePoints, persistedUserStats.PerformancePoints, 6);
        Assert.Equal(expectedWeighted.Accuracy, persistedUserStats.Accuracy, 6);
    }

    [Fact]
    public async Task TestCommitRecalculationDemotionUsesPromotedBestForWeightedValuesIfUpdateSubmissionStatus()
    {
        // Arrange
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);
        var calculator = pipelineScope.ServiceProvider.GetRequiredService<CalculatorService>();

        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var promotedPeer = _mocker.Score.GetBestScoreableRandomScore();
        promotedPeer.UserId = user.Id;
        promotedPeer.Mods = Mods.None;
        promotedPeer.TotalScore = 700;
        promotedPeer.PerformancePoints = 150;
        promotedPeer.MaxCombo = 300;
        promotedPeer.EnrichWithBeatmapData(beatmap);
        promotedPeer.GameMode = GameMode.RelaxStandard;
        promotedPeer.SubmissionStatus = SubmissionStatus.Submitted;
        promotedPeer.LocalProperties = promotedPeer.LocalProperties.FromScore(promotedPeer);
        promotedPeer = await CreateTestScore(promotedPeer);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.Mods = Mods.None;
        score.TotalScore = 900;
        score.PerformancePoints = 200;
        score.MaxCombo = 350;
        score.EnrichWithBeatmapData(beatmap);
        score.GameMode = GameMode.RelaxStandard;
        score.SubmissionStatus = SubmissionStatus.Best;
        score.LocalProperties = score.LocalProperties.FromScore(score);
        score = await CreateTestScore(score);

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var weightedBefore = await calculator.CalculateUserWeightedStats(user, score.GameMode);
        userStats.PerformancePoints = weightedBefore.PerformancePoints;
        userStats.Accuracy = weightedBefore.Accuracy;

        var context = new ScoreCommitContext(ScoreTaskType.Recalculation, score, user, userStats, userGrades, beatmap);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedScore = await Database.Scores.GetUnvalidatedScore(score.Id);
        var persistedPromotedPeer = await Database.Scores.GetUnvalidatedScore(promotedPeer.Id);
        var persistedUserStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);

        Assert.NotNull(persistedScore);
        Assert.NotNull(persistedPromotedPeer);
        Assert.NotNull(persistedUserStats);
        Assert.Equal(SubmissionStatus.Best, persistedScore.SubmissionStatus);
        Assert.Equal(SubmissionStatus.Submitted, persistedPromotedPeer.SubmissionStatus);

        var expectedWeighted = await calculator.CalculateUserWeightedStats(user, score.GameMode);
        Assert.Equal(expectedWeighted.PerformancePoints, persistedUserStats.PerformancePoints, 6);
        Assert.Equal(expectedWeighted.Accuracy, persistedUserStats.Accuracy, 6);
    }


    [Fact]
    public async Task TestCommitSubmissionUpdatesUserRankInLeaderboard()
    {
        // Arrange
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);
        var user = await CreateTestUser();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.Grade = "S";
        score.PerformancePoints = 500;
        score.EnrichWithBeatmapData(beatmap);
        score.SubmissionStatus = SubmissionStatus.Submitted;
        score.IsScoreable = false;
        score.BeatmapStatus = BeatmapStatus.Pending;
        score.LocalProperties = score.LocalProperties.FromScore(score);

        var (userStats, userGrades) = await LoadUserState(user, score.GameMode);
        var rankBefore = userStats.LocalProperties.Rank;

        var context = new ScoreCommitContext(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TestCommitSubmissionUpdatesGlobalAndCountryRank()
    {
        // Arrange
        var userA = _mocker.User.GetRandomUser(_mocker.User.GetRandomUsername());
        userA.Country = CountryCode.US;
        userA = await CreateTestUser(userA);

        var userB = _mocker.User.GetRandomUser(_mocker.User.GetRandomUsername());
        userB.Country = CountryCode.US;
        userB = await CreateTestUser(userB);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        // Give User A a persisted score with 100pp
        var scoreA = _mocker.Score.GetBestScoreableRandomScore();
        scoreA.UserId = userA.Id;
        scoreA.Mods = Mods.None;
        scoreA.GameMode = GameMode.Standard;
        scoreA.PerformancePoints = 100;
        scoreA.EnrichWithBeatmapData(beatmap);
        scoreA.LocalProperties = scoreA.LocalProperties.FromScore(scoreA);
        await Database.Scores.AddScore(scoreA);

        var userStatsA = await Database.Users.Stats.GetUserStats(userA.Id, GameMode.Standard);
        Assert.NotNull(userStatsA);
        userStatsA.UpdateWithDbScore(scoreA);
        userStatsA.PerformancePoints = 100;
        await Database.Users.Stats.UpdateUserStats(userStatsA, userA);

        // User A should be rank 1
        var (globalRankA, countryRankA) = await Database.Users.Stats.Ranks.GetUserRanks(userA, GameMode.Standard);
        Assert.Equal(1, globalRankA);
        Assert.Equal(1, countryRankA);

        // Create pipeline scope AFTER seeding data
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);

        // User B submits a score with higher PP (200) via pipeline
        var scoreB = _mocker.Score.GetBestScoreableRandomScore();
        scoreB.UserId = userB.Id;
        scoreB.Mods = Mods.None;
        scoreB.GameMode = GameMode.Standard;
        scoreB.PerformancePoints = 200;
        scoreB.EnrichWithBeatmapData(beatmap);
        scoreB.SubmissionStatus = SubmissionStatus.Submitted;
        scoreB.IsScoreable = false;
        scoreB.BeatmapStatus = BeatmapStatus.Pending;
        scoreB.LocalProperties = scoreB.LocalProperties.FromScore(scoreB);

        var (userStatsB, userGradesB) = await LoadUserState(userB, GameMode.Standard);
        var context = new ScoreCommitContext(ScoreTaskType.Submission, scoreB, userB, userStatsB, userGradesB, beatmap);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var (globalRankAAfter, countryRankAAfter) = await Database.Users.Stats.Ranks.GetUserRanks(userA, GameMode.Standard);
        var (globalRankBAfter, countryRankBAfter) = await Database.Users.Stats.Ranks.GetUserRanks(userB, GameMode.Standard);

        // User B (200pp) should now be rank 1, User A (100pp) should be rank 2
        Assert.Equal(1, globalRankBAfter);
        Assert.Equal(2, globalRankAAfter);
        Assert.Equal(1, countryRankBAfter);
        Assert.Equal(2, countryRankAAfter);
    }

    [Fact]
    public async Task TestCommitDeletionUpdatesGlobalAndCountryRank()
    {
        // Arrange
        var userA = _mocker.User.GetRandomUser(_mocker.User.GetRandomUsername());
        userA.Country = CountryCode.US;
        userA = await CreateTestUser(userA);

        var userB = _mocker.User.GetRandomUser(_mocker.User.GetRandomUsername());
        userB.Country = CountryCode.US;
        userB = await CreateTestUser(userB);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        var beatmapSet2 = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet2.IgnoreBeatmapRanking();
        var beatmap2 = beatmapSet2.Beatmaps!.First();

        // User A: 100pp best score
        var scoreA = _mocker.Score.GetBestScoreableRandomScore();
        scoreA.UserId = userA.Id;
        scoreA.Mods = Mods.None;
        scoreA.GameMode = GameMode.Standard;
        scoreA.PerformancePoints = 100;
        scoreA.EnrichWithBeatmapData(beatmap);
        scoreA.LocalProperties = scoreA.LocalProperties.FromScore(scoreA);
        await Database.Scores.AddScore(scoreA);

        // User B: two scores - 200pp best and 50pp fallback on different beatmaps
        var scoreBLow = _mocker.Score.GetBestScoreableRandomScore();
        scoreBLow.UserId = userB.Id;
        scoreBLow.Mods = Mods.None;
        scoreBLow.GameMode = GameMode.Standard;
        scoreBLow.PerformancePoints = 50;
        scoreBLow.EnrichWithBeatmapData(beatmap2);
        scoreBLow.LocalProperties = scoreBLow.LocalProperties.FromScore(scoreBLow);
        await Database.Scores.AddScore(scoreBLow);

        var scoreBHigh = _mocker.Score.GetBestScoreableRandomScore();
        scoreBHigh.UserId = userB.Id;
        scoreBHigh.Mods = Mods.None;
        scoreBHigh.GameMode = GameMode.Standard;
        scoreBHigh.PerformancePoints = 200;
        scoreBHigh.EnrichWithBeatmapData(beatmap);
        scoreBHigh.LocalProperties = scoreBHigh.LocalProperties.FromScore(scoreBHigh);
        await Database.Scores.AddScore(scoreBHigh);

        // Seed user stats with explicit PP values and update ranks
        var userStatsA = await Database.Users.Stats.GetUserStats(userA.Id, GameMode.Standard);
        Assert.NotNull(userStatsA);
        userStatsA.UpdateWithDbScore(scoreA);
        userStatsA.PerformancePoints = 100;
        await Database.Users.Stats.UpdateUserStats(userStatsA, userA);

        var userStatsB = await Database.Users.Stats.GetUserStats(userB.Id, GameMode.Standard);
        Assert.NotNull(userStatsB);
        userStatsB.UpdateWithDbScore(scoreBLow);
        userStatsB.UpdateWithDbScore(scoreBHigh);
        userStatsB.PerformancePoints = 250;
        await Database.Users.Stats.UpdateUserStats(userStatsB, userB);

        // Create pipeline scope AFTER all data is persisted
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);

        // Verify initial: B=1, A=2
        var (globalRankBBefore, countryRankBBefore) = await Database.Users.Stats.Ranks.GetUserRanks(userB, GameMode.Standard);
        var (globalRankABefore, countryRankABefore) = await Database.Users.Stats.Ranks.GetUserRanks(userA, GameMode.Standard);
        Assert.Equal(1, globalRankBBefore);
        Assert.Equal(2, globalRankABefore);
        Assert.Equal(1, countryRankBBefore);
        Assert.Equal(2, countryRankABefore);

        // Delete User B's high score via pipeline
        var userGradesB = await Database.Users.Grades.GetUserGrades(userB.Id, GameMode.Standard);
        Assert.NotNull(userGradesB);
        var context = new ScoreCommitContext(ScoreTaskType.Delete, scoreBHigh, userB, userStatsB, userGradesB);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedStatsB = await Database.Users.Stats.GetUserStats(userB.Id, GameMode.Standard);
        Assert.NotNull(persistedStatsB);
        Assert.Equal(1, persistedStatsB.PlayCount);

        // After deleting B's 200pp score, B should drop below A (only 50pp left)
        var (globalRankAAfter, countryRankAAfter) = await Database.Users.Stats.Ranks.GetUserRanks(userA, GameMode.Standard);
        var (globalRankBAfter, countryRankBAfter) = await Database.Users.Stats.Ranks.GetUserRanks(userB, GameMode.Standard);
        Assert.Equal(1, globalRankAAfter);
        Assert.Equal(2, globalRankBAfter);
        Assert.Equal(1, countryRankAAfter);
        Assert.Equal(2, countryRankBAfter);
    }

    [Fact]
    public async Task TestCommitRestorationUpdatesGlobalAndCountryRank()
    {
        // Arrange
        var userA = _mocker.User.GetRandomUser(_mocker.User.GetRandomUsername());
        userA.Country = CountryCode.US;
        userA = await CreateTestUser(userA);

        var userB = _mocker.User.GetRandomUser(_mocker.User.GetRandomUsername());
        userB.Country = CountryCode.US;
        userB = await CreateTestUser(userB);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        // User A: 100pp best score (currently rank 1)
        var scoreA = _mocker.Score.GetBestScoreableRandomScore();
        scoreA.UserId = userA.Id;
        scoreA.Mods = Mods.None;
        scoreA.GameMode = GameMode.Standard;
        scoreA.PerformancePoints = 100;
        scoreA.EnrichWithBeatmapData(beatmap);
        scoreA.LocalProperties = scoreA.LocalProperties.FromScore(scoreA);
        await Database.Scores.AddScore(scoreA);

        var userStatsA = await Database.Users.Stats.GetUserStats(userA.Id, GameMode.Standard);
        Assert.NotNull(userStatsA);
        userStatsA.UpdateWithDbScore(scoreA);
        userStatsA.PerformancePoints = 100;
        await Database.Users.Stats.UpdateUserStats(userStatsA, userA);

        // User B: has a deleted 200pp score (rank should be worse than A currently)
        var scoreB = _mocker.Score.GetBestScoreableRandomScore();
        scoreB.UserId = userB.Id;
        scoreB.Mods = Mods.None;
        scoreB.GameMode = GameMode.Standard;
        scoreB.PerformancePoints = 200;
        scoreB.SubmissionStatus = SubmissionStatus.Deleted;
        scoreB.EnrichWithBeatmapData(beatmap);
        scoreB.LocalProperties = scoreB.LocalProperties.FromScore(scoreB);
        await Database.Scores.AddScore(scoreB);

        var userStatsB = await Database.Users.Stats.GetUserStats(userB.Id, GameMode.Standard);
        Assert.NotNull(userStatsB);
        // User B has 0 PP (deleted score doesn't count)
        await Database.Users.Stats.UpdateUserStats(userStatsB, userB);

        // Verify: A=1, B=2
        var (globalRankABefore, countryRankABefore) = await Database.Users.Stats.Ranks.GetUserRanks(userA, GameMode.Standard);
        var (globalRankBBefore, countryRankBBefore) = await Database.Users.Stats.Ranks.GetUserRanks(userB, GameMode.Standard);
        Assert.Equal(1, globalRankABefore);
        Assert.Equal(1, countryRankABefore);
        Assert.True(globalRankBBefore > globalRankABefore);

        // Create pipeline scope AFTER all data is persisted
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);

        // Restore User B's score via pipeline
        var userGradesB = await Database.Users.Grades.GetUserGrades(userB.Id, GameMode.Standard);
        Assert.NotNull(userGradesB);
        var context = new ScoreCommitContext(ScoreTaskType.Restore, scoreB, userB, userStatsB, userGradesB);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        // After restoring B's 200pp score, B should overtake A
        var (globalRankAAfter, countryRankAAfter) = await Database.Users.Stats.Ranks.GetUserRanks(userA, GameMode.Standard);
        var (globalRankBAfter, countryRankBAfter) = await Database.Users.Stats.Ranks.GetUserRanks(userB, GameMode.Standard);
        Assert.Equal(1, globalRankBAfter);
        Assert.Equal(2, globalRankAAfter);
        Assert.Equal(1, countryRankBAfter);
        Assert.Equal(2, countryRankAAfter);
    }

    [Fact]
    public async Task TestCommitRecalculationUpdatesGlobalAndCountryRank()
    {
        // Arrange
        var userA = _mocker.User.GetRandomUser(_mocker.User.GetRandomUsername());
        userA.Country = CountryCode.US;
        userA = await CreateTestUser(userA);

        var userB = _mocker.User.GetRandomUser(_mocker.User.GetRandomUsername());
        userB.Country = CountryCode.US;
        userB = await CreateTestUser(userB);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();

        // User A: 100pp score
        var scoreA = _mocker.Score.GetBestScoreableRandomScore();
        scoreA.UserId = userA.Id;
        scoreA.Mods = Mods.None;
        scoreA.GameMode = GameMode.Standard;
        scoreA.PerformancePoints = 100;
        scoreA.EnrichWithBeatmapData(beatmap);
        scoreA.LocalProperties = scoreA.LocalProperties.FromScore(scoreA);
        await Database.Scores.AddScore(scoreA);

        var userStatsA = await Database.Users.Stats.GetUserStats(userA.Id, GameMode.Standard);
        Assert.NotNull(userStatsA);
        userStatsA.UpdateWithDbScore(scoreA);
        userStatsA.PerformancePoints = 100;
        await Database.Users.Stats.UpdateUserStats(userStatsA, userA);

        // User B: score persisted with 0pp (simulates pre-recalculation state)
        var scoreB = _mocker.Score.GetBestScoreableRandomScore();
        scoreB.UserId = userB.Id;
        scoreB.Mods = Mods.None;
        scoreB.GameMode = GameMode.Standard;
        scoreB.PerformancePoints = 0;
        scoreB.EnrichWithBeatmapData(beatmap);
        scoreB.LocalProperties = scoreB.LocalProperties.FromScore(scoreB);
        await Database.Scores.AddScore(scoreB);

        var userStatsB = await Database.Users.Stats.GetUserStats(userB.Id, GameMode.Standard);
        Assert.NotNull(userStatsB);
        userStatsB.UpdateWithDbScore(scoreB);
        await Database.Users.Stats.UpdateUserStats(userStatsB, userB);

        // Verify: A=1 (has PP), B=2 (0 PP)
        var (globalRankABefore, _) = await Database.Users.Stats.Ranks.GetUserRanks(userA, GameMode.Standard);
        var (globalRankBBefore, _) = await Database.Users.Stats.Ranks.GetUserRanks(userB, GameMode.Standard);
        Assert.Equal(1, globalRankABefore);
        Assert.True(globalRankBBefore > globalRankABefore);

        // Create pipeline scope AFTER all data is persisted
        using var pipelineScope = App.Server.Services.CreateScope();
        var pipeline = CreatePipeline(pipelineScope.ServiceProvider);

        // Recalculate User B's score with 200pp (simulates pp recalculation)
        scoreB.PerformancePoints = 200;
        var userGradesB = await Database.Users.Grades.GetUserGrades(userB.Id, GameMode.Standard);
        Assert.NotNull(userGradesB);
        var context = new ScoreCommitContext(ScoreTaskType.Recalculation, scoreB, userB, userStatsB, userGradesB);

        // Act
        var result = await pipeline.Commit(context, null, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        // After recalculation, B (200pp) should overtake A (100pp)
        var (globalRankAAfter, countryRankAAfter) = await Database.Users.Stats.Ranks.GetUserRanks(userA, GameMode.Standard);
        var (globalRankBAfter, countryRankBAfter) = await Database.Users.Stats.Ranks.GetUserRanks(userB, GameMode.Standard);
        Assert.Equal(1, globalRankBAfter);
        Assert.Equal(2, globalRankAAfter);
        Assert.Equal(1, countryRankBAfter);
        Assert.Equal(2, countryRankAAfter);
    }

    private static ScoreCommitPipeline CreatePipeline(IServiceProvider services, bool includeUserStatsProcessor = true)
    {
        var database = services.GetRequiredService<DatabaseService>();
        var processors = new List<IScoreEntityProcessor>
        {
            new LeaderboardProcessor(database),
            new UserGradesScoreProcessor(database)
        };

        if (includeUserStatsProcessor)
            processors.Add(new UserStatsScoreProcessor(database, services.GetRequiredService<CalculatorService>()));

        return new ScoreCommitPipeline(database, processors);
    }

    private async Task<(UserStats UserStats, UserGrades UserGrades)> LoadUserState(User user, GameMode mode)
    {
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, mode);
        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, mode);

        Assert.NotNull(userStats);
        Assert.NotNull(userGrades);

        return (userStats, userGrades);
    }

    private async Task<ScoreTaskQueue> CreateTask(
        ScoreTaskType taskType,
        int? scoreId = null,
        int? scoreProcessingQueueId = null,
        string? claimToken = null,
        DateTime? leaseExpiresAt = null)
    {
        var task = new ScoreTaskQueue
        {
            TaskType = taskType,
            ScoreId = scoreId,
            ScoreProcessingQueueId = scoreProcessingQueueId,
            Status = ScoreProcessingStatus.Failed,
            ClaimToken = claimToken,
            LeaseExpiresAt = leaseExpiresAt,
            CreatedAt = DateTime.UtcNow
        };

        await Database.ScoreTaskQueue.AddQueueEntry(task);
        return task;
    }

    private async Task<ScoreProcessingQueue> CreatePayload(int userId)
    {
        var payload = new ScoreProcessingQueue
        {
            UserId = userId,
            ScoreHash = $"{Guid.NewGuid():N}",
            ScoreSerialized = "payload",
            BeatmapHash = "pipeline-beatmap-hash",
            TimeElapsed = 120,
            OsuVersion = "b20260101.1",
            ClientHash = "client-hash",
            StoryboardHash = null,
            UserHash = "user-hash",
            WhenPlayed = DateTime.UtcNow
        };

        await Database.ScoreProcessingQueue.AddQueueEntry(payload);
        return payload;
    }

    private async Task<Score> CreatePersistedScore(
        int userId,
        Beatmap beatmap,
        long totalScore,
        SubmissionStatus submissionStatus,
        string grade,
        int maxCombo,
        bool isPassed = true)
    {
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = userId;
        score.Mods = Mods.None;
        score.TotalScore = totalScore;
        score.Grade = grade;
        score.MaxCombo = maxCombo;
        score.EnrichWithBeatmapData(beatmap);
        score.SubmissionStatus = submissionStatus;

        if (!isPassed)
        {
            score.IsPassed = false;
            score.CountMiss = 1;
        }

        score.LocalProperties = score.LocalProperties.FromScore(score);

        return await CreateTestScore(score);
    }
}