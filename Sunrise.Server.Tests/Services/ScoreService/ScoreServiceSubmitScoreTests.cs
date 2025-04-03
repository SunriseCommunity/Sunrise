using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services;
using Sunrise.Tests.Services.Mock;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Server.Tests.Services.ScoreService;

public class ScoreServiceSubmitScoreRedisTests() : DatabaseTest(true)
{
    private readonly MockService _mocker = new();
    private readonly ReplayService _replayService = new();

    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Select(mode => new object[]
        {
            mode
        });
    }

    [Fact]
    public async Task TestSuccessfulSubmitScore()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var (replay, beatmapId) = GetValidTestReplay();

        var score = replay.GetScore();
        score.BeatmapId = beatmapId;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        var databaseScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(databaseScore);

        Assert.Equal(SubmissionStatus.Best, databaseScore.SubmissionStatus);
    }
    
    [Fact]
    public async Task TestSuccessfulSubmitScoreForBeatmapWithCustomStatusRanked()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var (replay, beatmapId) = GetValidTestReplay();

        var score = replay.GetScore();
        score.BeatmapId = beatmapId;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);
        beatmap.StatusString = "pending";

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");
        
        await Database.CustomBeatmapStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatus.Ranked,
            BeatmapHash = beatmap.Checksum,
            BeatmapSetId = beatmapSet.Id,
            UpdatedByUserId = session.UserId
        });

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        var databaseScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(databaseScore);

        Assert.Equal(SubmissionStatus.Best, databaseScore.SubmissionStatus);
        Assert.Equal(BeatmapStatus.Ranked, databaseScore.BeatmapStatus);
    }
    
    [Fact]
    public async Task TestSuccessfulSubmitScoreForBeatmapWithCustomStatusDerank()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var (replay, beatmapId) = GetValidTestReplay();

        var score = replay.GetScore();
        score.BeatmapId = beatmapId;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);
        beatmap.StatusString = "ranked";

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");
        
        await Database.CustomBeatmapStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatus.Pending,
            BeatmapHash = beatmap.Checksum,
            BeatmapSetId = beatmapSet.Id,
            UpdatedByUserId = session.UserId
        });

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.Contains("error", resultString);

        var databaseScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(databaseScore);

        Assert.Equal(SubmissionStatus.Submitted, databaseScore.SubmissionStatus);
        Assert.Equal(BeatmapStatus.Pending, databaseScore.BeatmapStatus);
    }

    [Fact]
    public async Task TestSuccessfulUnlockMedalAfterScoreSubmission()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var (replay, beatmapId) = GetValidTestReplay();

        var score = replay.GetScore();
        score.BeatmapId = beatmapId;
        score.Mods |= Mods.DoubleTime;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        var userUnlockedMedals = await Database.Users.Medals.GetUserMedals(session.UserId);
        Assert.NotEmpty(userUnlockedMedals);
    }

    [Fact]
    public async Task TestSuccessfulUpdateUserGradesAfterScoreSubmission()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var (replay, beatmapId) = GetValidTestReplay();

        var score = replay.GetScore();
        score.Grade = "S";
        score.BeatmapId = beatmapId;
        score.Mods |= Mods.DoubleTime;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        var userGrades = await Database.Users.Grades.GetUserGrades(session.UserId, score.GameMode);

        Assert.NotNull(userGrades);
        Assert.Equal(1, userGrades.CountS);
    }

    [Fact]
    public async Task TestSuccessfulUploadReplayUponSubmitScore()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var (replay, beatmapId) = GetValidTestReplay();

        var score = replay.GetScore();
        score.BeatmapId = beatmapId;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        score = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(score);
        Assert.NotNull(score.ReplayFileId);

        var replayFile = await Database.Scores.Files.GetReplayFile(score.ReplayFileId.Value);
        Assert.NotNull(replayFile);
    }

    [Fact]
    public async Task TestUpdateUserStatsUponSubmitScore()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var userStatsBeforeScore = await Database.Users.Stats.GetUserStats(session.UserId, GameMode.Standard);
        if (userStatsBeforeScore == null)
            throw new Exception("User stats are null");

        var (replay, beatmapId) = GetValidTestReplay();

        var score = replay.GetScore();
        score.BeatmapId = beatmapId;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        var userStatsAfterScore = await Database.Users.Stats.GetUserStats(session.UserId, score.GameMode);
        if (userStatsAfterScore == null)
            throw new Exception("User stats are null");

        Assert.NotEqual(userStatsBeforeScore.PerformancePoints, userStatsAfterScore.PerformancePoints);
    }

    [Fact]
    public async Task TestUserRestrictByPpThresholdUponSubmitScore()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.ToVanillaScore();

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        EnvManager.Set("Moderation:BannablePpThreshold", "0");

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.Contains("error", resultString);

        var isRestricted = await Database.Users.Moderation.IsUserRestricted(session.UserId);
        Assert.True(isRestricted);

        var restrictionReason = await Database.Users.Moderation.GetActiveRestrictionReason(session.UserId);
        Assert.Contains("submitting impossible score", restrictionReason);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestUserIgnoreRestrictionByPpThresholdIfNotVanillaGamemodeUponSubmitScore(GameMode gameMode)
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = gameMode;
        score.Mods = gameMode.GetGamemodeMods();

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        EnvManager.Set("Moderation:BannablePpThreshold", "0");

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        var isRestricted = await Database.Users.Moderation.IsUserRestricted(session.UserId);

        if (gameMode.IsVanillaGameMode())
        {
            Assert.Contains("error", resultString);
            Assert.True(isRestricted);
        }
        else
        {
            Assert.DoesNotContain("error", resultString);
            Assert.False(isRestricted);
        }
    }

    [Theory]
    [InlineData(Mods.Target)]
    [InlineData(Mods.Random)]
    [InlineData(Mods.KeyCoop)]
    [InlineData(Mods.Cinema)]
    [InlineData(Mods.Autoplay)]
    public async Task TestIgnoreSubmitScoreWithInvalidMod(Mods mods)
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.ToVanillaScore();
        score.Mods = mods;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.Contains("error", resultString);

        var dbScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.Null(dbScore);
    }

    [Fact]
    public async Task TestIgnoreSubmitScoreWithNonStandardMods()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.Mods = Mods.ScoreV2 | Mods.Relax;
        score.GameMode = score.GameMode.EnrichWithMods(score.Mods);

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.Contains("error", resultString);

        var dbScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.Null(dbScore);
    }

    [Fact]
    public async Task TestIgnoreSubmitScoreIfHashAlreadyIncludedInDatabase()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        await Database.Scores.AddScore(score);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.Contains("error", resultString);
    }

    [Fact]
    public async Task TestIgnoreSubmitScoreIfInvalidReplay()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(1),
            null
        );

        // Assert
        Assert.Contains("error", resultString);
    }

    [Fact]
    public async Task TestUpdateUserStatsForNonScoreableScoreUponSubmitScore()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.IsScoreable = false;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var timeElapsed = _mocker.GetRandomInteger();

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            timeElapsed,
            timeElapsed,
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.Contains("error", resultString); // Doesn't render chart for non-scoreable scores

        var userStats = await Database.Users.Stats.GetUserStats(session.UserId, score.GameMode);
        if (userStats == null)
            throw new Exception("User stats are null");

        var totalHits = score.Count300 + score.Count100 + score.Count50;
        if ((GameMode)userStats.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania)
            totalHits += score.CountGeki + score.CountKatu;

        Assert.Equal(userStats.TotalScore, score.TotalScore);
        Assert.Equal(userStats.TotalHits, totalHits);
        Assert.Equal(userStats.PlayTime, timeElapsed);
        Assert.Equal(1, userStats.PlayCount);

        Assert.Equal(0, userStats.MaxCombo);
        Assert.Equal(0, userStats.PerformancePoints);
    }

    [Fact]
    public async Task TestUserRestrictInvalidChecksumUponSubmitScore()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithSessionData(session);
        score.ScoreHash = _mocker.GetRandomString();

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.Contains("error", resultString);

        var isRestricted = await Database.Users.Moderation.IsUserRestricted(session.UserId);
        Assert.True(isRestricted);

        var restrictionReason = await Database.Users.Moderation.GetActiveRestrictionReason(session.UserId);
        Assert.Contains("Invalid checksums on score submission", restrictionReason);
    }

    [Fact]
    public async Task TestUponSubmittingBetterScoreThanPreviousOneUpdateSubmissionStatus()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var oldScore = _mocker.Score.GetBestScoreableRandomScore();
        oldScore.SubmissionStatus = SubmissionStatus.Best;
        oldScore.PerformancePoints = -1;

        oldScore.EnrichWithSessionData(session);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = oldScore.GameMode;
        score.Mods = oldScore.Mods;
        score.BeatmapId = oldScore.BeatmapId;
        score.BeatmapHash = oldScore.BeatmapHash;

        score.TotalScore = oldScore.TotalScore + 1;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        await Database.Scores.AddScore(oldScore);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        var dbOldScore = await Database.Scores.GetScore(oldScore.ScoreHash);
        Assert.NotNull(dbOldScore);
        Assert.Equal(SubmissionStatus.Submitted, dbOldScore.SubmissionStatus);

        var (bestBeatmapScore, _) = await Database.Scores.GetBeatmapScores(score.BeatmapHash, score.GameMode);
        Assert.Contains(bestBeatmapScore, x => x.ScoreHash == score.ScoreHash);
    }

    [Fact]
    public async Task TestUponSubmittingBetterScoreThanPreviousOneUpdateUserGrades()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var oldScore = _mocker.Score.GetBestScoreableRandomScore();
        oldScore.Grade = "A";
        oldScore.SubmissionStatus = SubmissionStatus.Best;
        oldScore.PerformancePoints = -1;

        oldScore.EnrichWithSessionData(session);


        var userGrades = await Database.Users.Grades.GetUserGrades(oldScore.UserId, oldScore.GameMode);
        if (userGrades == null)
            throw new Exception("UserGrades is null");

        userGrades = _mocker.User.SetRandomUserGrades(userGrades);
        userGrades.CountA++;

        var arrangeUserGradesResult = await Database.Users.Grades.UpdateUserGrades(userGrades);

        if (arrangeUserGradesResult.IsFailure)
            throw new Exception(arrangeUserGradesResult.Error);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = oldScore.GameMode;
        score.Mods = oldScore.Mods;
        score.BeatmapId = oldScore.BeatmapId;
        score.BeatmapHash = oldScore.BeatmapHash;
        score.Grade = "B";

        score.TotalScore = oldScore.TotalScore + 1;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        await Database.Scores.AddScore(oldScore);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        var updatedUserGrades = await Database.Users.Grades.GetUserGrades(session.UserId, oldScore.GameMode);

        Assert.NotNull(updatedUserGrades);
        userGrades.User = null!; // Ignore for comparison

        userGrades.CountB++;
        userGrades.CountA--;

        Assert.Equivalent(userGrades, updatedUserGrades);
    }

    [Fact]
    public async Task TestUponSubmittingEqualScoreThanPreviousOneUpdateSubmissionStatus()
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var oldScore = _mocker.Score.GetBestScoreableRandomScore();
        oldScore.SubmissionStatus = SubmissionStatus.Best;

        oldScore.EnrichWithSessionData(session);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = oldScore.GameMode;
        score.Mods = oldScore.Mods;
        score.BeatmapId = oldScore.BeatmapId;
        score.BeatmapHash = oldScore.BeatmapHash;

        score.TotalScore = oldScore.TotalScore;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        await Database.Scores.AddScore(oldScore);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        var dbOldScore = await Database.Scores.GetScore(oldScore.ScoreHash);
        Assert.NotNull(dbOldScore);
        Assert.Equal(SubmissionStatus.Best, dbOldScore.SubmissionStatus);

        var (bestBeatmapScore, _) = await Database.Scores.GetBeatmapScores(score.BeatmapHash, score.GameMode);
        Assert.Contains(bestBeatmapScore, x => x.ScoreHash == oldScore.ScoreHash);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestUponSubmittingModdedAndUnmoddedScoreBothAreBestInTheirRespectiveLeaderboards(GameMode gameMode)
    {
        // Arrange
        var scoreService = Scope.ServiceProvider.GetRequiredService<Server.Services.ScoreService>();

        var session = await CreateTestSession();

        var (scoreData, beatmapId) = GetValidTestReplay();
        var beatmapHash = scoreData.GetScore().BeatmapHash;

        var moddedScore = _mocker.Score.GetBestScoreableRandomScore();
        moddedScore.BeatmapId = beatmapId;
        moddedScore.BeatmapHash = beatmapHash;
        moddedScore.GameMode = gameMode;
        moddedScore.Mods = Mods.DoubleTime | moddedScore.GameMode.GetGamemodeMods();
        moddedScore.SubmissionStatus = SubmissionStatus.Best;
        moddedScore.PerformancePoints = -1;

        moddedScore.EnrichWithSessionData(session);

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = moddedScore.GameMode;
        score.Mods = moddedScore.GameMode.GetGamemodeMods();
        score.BeatmapId = moddedScore.BeatmapId;
        score.BeatmapHash = moddedScore.BeatmapHash;
        score.TotalScore = moddedScore.TotalScore + 1;

        score.ToBestPerformance();
        score.PerformancePoints = moddedScore.PerformancePoints + 1;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        await Database.Scores.AddScore(moddedScore);

        // Act
        var resultString = await scoreService.SubmitScore(
            session,
            score.ToScoreString(),
            score.BeatmapHash,
            _mocker.GetRandomInteger(),
            _mocker.GetRandomInteger(),
            _mocker.GetRandomString(),
            session.Attributes.UserHash,
            _replayService.GenerateReplayFormFile(),
            null
        );

        // Assert
        Assert.DoesNotContain("error", resultString);

        var dbModdedScore = await Database.Scores.GetScore(moddedScore.ScoreHash);
        Assert.NotNull(dbModdedScore);
        Assert.Equal(SubmissionStatus.Best, dbModdedScore.SubmissionStatus);

        var dbScore = await Database.Scores.GetScore(score.ScoreHash);
        Assert.NotNull(dbScore);
        Assert.Equal(SubmissionStatus.Best, dbScore.SubmissionStatus);

        // Best beatmap score should belong to non-modded score due to the fact that it has higher total score
        var (bestBeatmapScore, _) = await Database.Scores.GetBeatmapScores(score.BeatmapHash, score.GameMode);
        Assert.Contains(bestBeatmapScore, x => x.ScoreHash == score.ScoreHash);
    }
}