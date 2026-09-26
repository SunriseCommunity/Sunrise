using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils.Processing;
using Xunit;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using Mods = osu.Shared.Mods;

namespace Sunrise.Processing.Tests.Scores.Processors;

[Collection("Integration tests collection")]
public class MedalScoreProcessorTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestOnNewSubmissionWithRankedPassedScoreUnlocksSeededSkillMedal()
    {
        // Arrange
        var processor = new MedalScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        var score = CreateScore(user);
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        beatmap.DifficultyRating = 1;
        var context = await CreateContext(ScoreTaskType.Submission, score, user, userStats, beatmap, beatmapSet);

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.NotNull(context.UnlockedMedals);
        Assert.Contains(context.UnlockedMedals, m => m.Id == 1);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Contains(userMedals, m => m.MedalId == 1);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithRankedPassedNoFailScoreUnlocksNoFailModIntroductionMedal()
    {
        // Arrange
        var processor = new MedalScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        var score = CreateScore(user);
        score.Mods = Mods.NoFail;
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        var context = await CreateContext(ScoreTaskType.Submission, score, user, userStats, beatmap, beatmapSet);

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.NotNull(context.UnlockedMedals);
        Assert.Contains(context.UnlockedMedals, m => m.Id == 97);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Contains(userMedals, m => m.MedalId == 97);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithRankedPassedScoreUnlocksMultipleMedals()
    {
        // Arrange
        var processor = new MedalScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        var score = CreateScore(user);
        score.Mods = Mods.DoubleTime;
        score.MaxCombo = 500;

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        var context = await CreateContext(ScoreTaskType.Submission, score, user, userStats, beatmap, beatmapSet);

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.NotNull(context.UnlockedMedals);
        Assert.Contains(context.UnlockedMedals, m => m.Id == 92);
        Assert.Contains(context.UnlockedMedals, m => m.Id == 21);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Contains(userMedals, m => m.MedalId == 92);
        Assert.Contains(userMedals, m => m.MedalId == 21);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithFailedScoreDoesNotUnlockAnyMedals()
    {
        // Arrange
        var processor = new MedalScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        var score = CreateScore(user);
        score.IsPassed = false;

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        var context = await CreateContext(ScoreTaskType.Submission, score, user, userStats, beatmap, beatmapSet);

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.NotNull(context.UnlockedMedals);
        Assert.Empty(context.UnlockedMedals);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Empty(userMedals);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithUnscoreableBeatmapDoesNotUnlockAnyMedals()
    {
        // Arrange
        var processor = new MedalScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        var score = CreateScore(user);
        score.Mods = Mods.NoFail;

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockGraveyardBeatmapWithSetForScore(score);
        var context = await CreateContext(ScoreTaskType.Submission, score, user, userStats, beatmap, beatmapSet);

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.NotNull(context.UnlockedMedals);
        Assert.Empty(context.UnlockedMedals);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Empty(userMedals);
    }

    [Fact]
    public async Task TestOnNewSubmissionWithPreviouslyUnlockedMedalDoesNotDuplicateIt()
    {
        // Arrange
        var processor = new MedalScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        var score = CreateScore(user);
        score.Mods = Mods.NoFail;

        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);

        await Database.Users.Medals.UnlockMedals(user.Id, [97]);
        var context = await CreateContext(ScoreTaskType.Submission, score, user, userStats, beatmap, beatmapSet);

        // Act
        await processor.OnNewSubmission(context);

        // Assert
        Assert.NotNull(context.UnlockedMedals);
        Assert.DoesNotContain(context.UnlockedMedals, m => m.Id == 97);

        var userMedals = await Database.Users.Medals.GetUserMedals(user.Id);
        Assert.Contains(userMedals, m => m.MedalId == 97);
    }

    [Fact]
    public async Task TestOnRecalculationWithRankedPassedScoreUnlocksMedals()
    {
        // Arrange
        var processor = new MedalScoreProcessor(Database);
        var user = await CreateTestUser();
        var userStats = await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard);
        Assert.NotNull(userStats);

        var score = CreateScore(user);
        score.Mods = Mods.NoFail;
        var (beatmapSet, beatmap) = await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
        var context = await CreateContext(ScoreTaskType.Recalculation, score, user, userStats, beatmap, beatmapSet);

        // Act
        await processor.OnRecalculation(context);

        // Assert
        Assert.NotNull(context.UnlockedMedals);
        Assert.Contains(context.UnlockedMedals, m => m.Id == 97);
    }

    private Score CreateScore(User user)
    {
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.Standard;
        score.EnrichWithUserData(user);

        return score;
    }

    private async Task<ScoreCommitContext> CreateContext(
        ScoreTaskType taskType,
        Score score,
        User user,
        UserStats userStats,
        Beatmap beatmap,
        BeatmapSet beatmapSet)
    {
        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, score.GameMode);
        Assert.NotNull(userGrades);

        return ScoreCommitContextFactory.Create(
            taskType,
            score,
            user,
            userStats,
            userGrades,
            beatmap,
            beatmapSet,
            originalState: ScoreStateSnapshot.Capture(score));
    }
}