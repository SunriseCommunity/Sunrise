using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Extensions;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Extensions;
using Sunrise.Server.Tests.Core.Services;
using Sunrise.Server.Tests.Core.Services.Mock;
using GameMode = Sunrise.Server.Types.Enums.GameMode;
using SubmissionStatus = Sunrise.Server.Types.Enums.SubmissionStatus;

namespace Sunrise.Server.Tests.Services.ScoreService;

public class ScoreServiceSubmitScoreTests() : DatabaseTest(true)
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
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var (replay, beatmapId) = await GetValidTestReplay();

        var score = replay.GetScore();
        score.BeatmapId = beatmapId;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var databaseScore = await database.ScoreService.GetScore(score.ScoreHash);
        Assert.NotNull(databaseScore);

        Assert.Equal(SubmissionStatus.Best, databaseScore.SubmissionStatus);
    }

    [Fact]
    public async Task TestSuccessfulUploadReplayUponSubmitScore()
    {
        // Arrange
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var (replay, beatmapId) = await GetValidTestReplay();

        var score = replay.GetScore();
        score.BeatmapId = beatmapId;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        score = await database.ScoreService.GetScore(score.ScoreHash);
        Assert.NotNull(score);
        Assert.NotNull(score.ReplayFileId);

        var replayFile = await database.ScoreService.Files.GetReplay(score.ReplayFileId.Value);
        Assert.NotNull(replayFile);
    }

    [Fact]
    public async Task TestUpdateUserStatsUponSubmitScore()
    {
        // Arrange
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var userStatsBeforeScore = await database.UserService.Stats.GetUserStats(session.User.Id, GameMode.Standard);
        if (userStatsBeforeScore == null)
            throw new Exception("User stats are null");

        var (replay, beatmapId) = await GetValidTestReplay();

        var score = replay.GetScore();
        score.BeatmapId = beatmapId;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var userStatsAfterScore = await database.UserService.Stats.GetUserStats(session.User.Id, score.GameMode);
        if (userStatsAfterScore == null)
            throw new Exception("User stats are null");

        Assert.NotEqual(userStatsBeforeScore.PerformancePoints, userStatsAfterScore.PerformancePoints);
    }

    [Fact]
    public async Task TestUserRestrictByPpThresholdUponSubmitScore()
    {
        // Arrange
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.ToVanillaScore();

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        EnvManager.Set("Moderation:BannablePpThreshold", "0");

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var isRestricted = await database.UserService.Moderation.IsRestricted(session.User.Id);
        Assert.True(isRestricted);

        var restrictionReason = await database.UserService.Moderation.GetRestrictionReason(session.User.Id);
        Assert.Contains("submitting impossible score", restrictionReason);
    }

    [Fact]
    public async Task TestUserIgnoreRestrictionByPpThresholdIfNotVanillaGamemodeUponSubmitScore()
    {
        // Arrange
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.RelaxStandard;
        score.Mods = Mods.Relax;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        EnvManager.Set("Moderation:BannablePpThreshold", "0");

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var isRestricted = await database.UserService.Moderation.IsRestricted(session.User.Id);
        Assert.False(isRestricted);
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
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.ToVanillaScore();
        score.Mods = mods;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var dbScore = await database.ScoreService.GetScore(score.ScoreHash);
        Assert.Null(dbScore);
    }

    [Fact]
    public async Task TestIgnoreSubmitScoreWithNonStandardMods()
    {
        // Arrange
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.Mods = Mods.ScoreV2 | Mods.Relax;
        score.GameMode = score.GameMode.EnrichWithMods(score.Mods);

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var dbScore = await database.ScoreService.GetScore(score.ScoreHash);
        Assert.Null(dbScore);
    }

    [Fact]
    public async Task TestIgnoreSubmitScoreIfHashAlreadyIncludedInDatabase()
    {
        // Arrange
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        await database.ScoreService.InsertScore(score);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.IsScoreable = false;

        score.EnrichWithSessionData(session);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        var timeElapsed = _mocker.GetRandomInteger();

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var userStats = await database.UserService.Stats.GetUserStats(session.User.Id, score.GameMode);
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
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithSessionData(session);
        score.ScoreHash = _mocker.GetRandomString();

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.EnrichWithScoreData(score);

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var isRestricted = await database.UserService.Moderation.IsRestricted(session.User.Id);
        Assert.True(isRestricted);

        var restrictionReason = await database.UserService.Moderation.GetRestrictionReason(session.User.Id);
        Assert.Contains("Invalid checksums on score submission", restrictionReason);
    }

    [Fact]
    public async Task TestUponSubmittingBetterScoreThanPreviousOneUpdateSubmissionStatus()
    {
        // Arrange
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var oldScore = _mocker.Score.GetBestScoreableRandomScore();
        oldScore.SubmissionStatus = SubmissionStatus.Best;

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
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        await database.ScoreService.InsertScore(oldScore);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var dbOldScore = await database.ScoreService.GetScore(oldScore.ScoreHash);
        Assert.NotNull(dbOldScore);
        Assert.Equal(SubmissionStatus.Submitted, dbOldScore.SubmissionStatus);

        var bestBeatmapScore = await database.ScoreService.GetBeatmapScores(score.BeatmapHash, score.GameMode);
        Assert.Contains(bestBeatmapScore, x => x.ScoreHash == score.ScoreHash);
    }

    [Fact]
    public async Task TestUponSubmittingEqualScoreThanPreviousOneUpdateSubmissionStatus()
    {
        // Arrange
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

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
        await _mocker.Redis.MockBeatmapFile(beatmap.Id);

        await database.ScoreService.InsertScore(oldScore);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var dbOldScore = await database.ScoreService.GetScore(oldScore.ScoreHash);
        Assert.NotNull(dbOldScore);
        Assert.Equal(SubmissionStatus.Best, dbOldScore.SubmissionStatus);

        var bestBeatmapScore = await database.ScoreService.GetBeatmapScores(score.BeatmapHash, score.GameMode);
        Assert.Contains(bestBeatmapScore, x => x.ScoreHash == oldScore.ScoreHash);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestUponSubmittingModdedAndUnmoddedScoreBothAreBestInTheirRespectiveLeaderboards(GameMode gameMode)
    {
        // Arrange
        using var app = new SunriseServerFactory().CreateClient();
        var session = await CreateTestSession();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var (scoreData, beatmapId) = await GetValidTestReplay();
        var beatmapHash = scoreData.GetScore().BeatmapHash;

        var moddedScore = _mocker.Score.GetBestScoreableRandomScore();
        moddedScore.BeatmapId = beatmapId;
        moddedScore.BeatmapHash = beatmapHash;
        moddedScore.GameMode = gameMode;
        moddedScore.Mods = Mods.DoubleTime | moddedScore.GameMode.GetGamemodeMods();
        moddedScore.SubmissionStatus = SubmissionStatus.Best;
        moddedScore.PerformancePoints = 0;

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

        await database.ScoreService.InsertScore(moddedScore);

        // Act
        var resultString = await Server.Services.ScoreService.SubmitScore(
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

        var dbModdedScore = await database.ScoreService.GetScore(moddedScore.ScoreHash);
        Assert.NotNull(dbModdedScore);
        Assert.Equal(SubmissionStatus.Best, dbModdedScore.SubmissionStatus);

        var dbScore = await database.ScoreService.GetScore(score.ScoreHash);
        Assert.NotNull(dbScore);
        Assert.Equal(SubmissionStatus.Best, dbScore.SubmissionStatus);

        // Best beatmap score should belong to non-modded score due to the fact that it has higher total score
        var bestBeatmapScore = await database.ScoreService.GetBeatmapScores(score.BeatmapHash, score.GameMode);
        Assert.Contains(bestBeatmapScore, x => x.ScoreHash == score.ScoreHash);
    }
}