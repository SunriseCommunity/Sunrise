using Sunrise.Processing.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Utils.Converters;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Xunit;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using Mods = osu.Shared.Mods;

namespace Sunrise.Processing.Tests.Utils;

public class ScoreSubmissionUtilTests : BaseTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public void TestGetNewFirstPlaceStringWithValidArgsReturnsFormattedMessage()
    {
        // Arrange
        var score = _mocker.Score.GetRandomScore();
        var user = _mocker.User.GetRandomUser();
        score.User = user;

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();

        var expectedMessage = $"[https://{Configuration.Domain}/user/{user.Id} {user.Username}] achieved #1 on {beatmap.GetBeatmapInGameChatString(beatmapSet)} {score.Mods.GetModsString()}| GameMode: {score.GameMode.ToVanillaGameMode()} | Acc: {score.Accuracy:0.00}% | {score.PerformancePoints:0.00}pp | {TimeConverter.SecondsToString(beatmap.TotalLength)} | {beatmap.DifficultyRating:0.00} ★";

        // Act
        var result = ScoreSubmissionUtil.GetNewFirstPlaceString(score, beatmapSet, beatmap);

        // Assert
        Assert.Equal(expectedMessage, result);
    }

    [Fact]
    public void TestGetNewFirstPlaceStringWithoutUserThrowsNullReferenceException()
    {
        // Arrange
        var score = _mocker.Score.GetRandomScore();
        score.User = null;

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => ScoreSubmissionUtil.GetNewFirstPlaceString(score, beatmapSet, beatmap));
    }

    [Fact]
    public void TestUpdateSubmissionStatusWithFailedScoreReturnsFailedStatus()
    {
        // Arrange
        var score = CreateScore(isPassed: false, mods: Mods.None);

        // Act
        score.UpdateSubmissionStatus(null);

        // Assert
        Assert.Equal(SubmissionStatus.Failed, score.SubmissionStatus);
    }

    [Fact]
    public void TestUpdateSubmissionStatusWithUnscoreableScoreReturnsSubmittedStatus()
    {
        // Arrange
        var score = CreateScore(isScoreable: false, beatmapStatus: BeatmapStatus.Pending);

        // Act
        score.UpdateSubmissionStatus(null);

        // Assert
        Assert.Equal(SubmissionStatus.Submitted, score.SubmissionStatus);
    }

    [Fact]
    public void TestUpdateSubmissionStatusWithFirstScoreReturnsBestStatus()
    {
        // Arrange
        var score = CreateScore(totalScore: 1500, submissionStatus: SubmissionStatus.Unknown);

        // Act
        score.UpdateSubmissionStatus(null);

        // Assert
        Assert.Equal(SubmissionStatus.Best, score.SubmissionStatus);
    }

    [Fact]
    public void TestUpdateSubmissionStatusWithWorseScoreReturnsSubmittedStatus()
    {
        // Arrange
        var score = CreateScore(totalScore: 900, submissionStatus: SubmissionStatus.Unknown);
        var previousBest = CreateScore(totalScore: 1000, submissionStatus: SubmissionStatus.Best);

        // Act
        score.UpdateSubmissionStatus(previousBest);

        // Assert
        Assert.Equal(SubmissionStatus.Submitted, score.SubmissionStatus);
    }

    [Fact]
    public void TestGetScoreSubmitResponseWithRankedBeatmapReturnsExpectedResponse()
    {
        // Arrange
        var beatmap = CreateBeatmap();
        var previousBeatmapBest = CreateScore(44, 1000, 300, 97, 90, leaderboardPosition: 5);
        var previousPerformanceBest = CreateScore(45, 950, 290, 96, 90, leaderboardPosition: 6);
        var newScore = CreateScore(55, 1200, leaderboardPosition: 1);

        var prevUserStats = CreateUserStats(5000, 1000, 300, 95, 200, 10);
        var userStats = CreateUserStats(6200, 1200, 400, 96, 210, 8);

        var previousPersonalBestScores = new UserPersonalBestScores(previousBeatmapBest, previousPerformanceBest);

        var expectedResponse =
            $"beatmapId:11|beatmapSetId:22|beatmapPlaycount:33|beatmapPasscount:44|approvedDate:2026-01-02\n" +
            $"chartId:beatmap|chartUrl:https://example/map|chartName:Beatmap Ranking|rankBefore:5|rankAfter:1|rankedScoreBefore:1000|rankedScoreAfter:1200|totalScoreBefore:1000|totalScoreAfter:1200|maxComboBefore:300|maxComboAfter:400|accuracyBefore:97|accuracyAfter:99|ppBefore:90|ppAfter:100|onlineScoreId:55\n" +
            $"chartId:overall|chartUrl:https://{Configuration.Domain}/user/77|chartName:Overall Ranking|rankBefore:10|rankAfter:8|rankedScoreBefore:1000|rankedScoreAfter:1200|totalScoreBefore:5000|totalScoreAfter:6200|maxComboBefore:300|maxComboAfter:400|accuracyBefore:95|accuracyAfter:96|ppBefore:200|ppAfter:210|achievements-new:new-medal";

        // Act
        var result = ScoreSubmissionUtil.GetScoreSubmitResponse(beatmap, userStats, prevUserStats, newScore, previousPersonalBestScores, "new-medal");

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public void TestGetScoreSubmitResponseWithLovedBeatmapHidesBeatmapPpValues()
    {
        // Arrange
        var beatmap = CreateBeatmap("loved");
        var previousBeatmapBest = CreateScore(44, 1000, 300, 97, 90, leaderboardPosition: 5);
        var previousPerformanceBest = CreateScore(45, 950, 290, 96, 90, leaderboardPosition: 6);
        var newScore = CreateScore(55, 1200, leaderboardPosition: 1);

        var prevUserStats = CreateUserStats(5000, 1000, 300, 95, 200, 10);
        var userStats = CreateUserStats(6200, 1200, 400, 96, 210, 8);

        var previousPersonalBestScores = new UserPersonalBestScores(previousBeatmapBest, previousPerformanceBest);

        var expectedResponse =
            $"beatmapId:11|beatmapSetId:22|beatmapPlaycount:33|beatmapPasscount:44|approvedDate:2026-01-02\n" +
            $"chartId:beatmap|chartUrl:https://example/map|chartName:Beatmap Ranking|rankBefore:5|rankAfter:1|rankedScoreBefore:1000|rankedScoreAfter:1200|totalScoreBefore:1000|totalScoreAfter:1200|maxComboBefore:300|maxComboAfter:400|accuracyBefore:97|accuracyAfter:99|ppBefore:|ppAfter:|onlineScoreId:55\n" +
            $"chartId:overall|chartUrl:https://{Configuration.Domain}/user/77|chartName:Overall Ranking|rankBefore:10|rankAfter:8|rankedScoreBefore:1000|rankedScoreAfter:1200|totalScoreBefore:5000|totalScoreAfter:6200|maxComboBefore:300|maxComboAfter:400|accuracyBefore:95|accuracyAfter:96|ppBefore:200|ppAfter:210|achievements-new:";

        // Act
        var result = ScoreSubmissionUtil.GetScoreSubmitResponse(beatmap, userStats, prevUserStats, newScore, previousPersonalBestScores);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Theory]
    [InlineData(Mods.Target)]
    [InlineData(Mods.Random)]
    [InlineData(Mods.KeyCoop)]
    [InlineData(Mods.Cinema)]
    [InlineData(Mods.Autoplay)]
    public void TestIsHasInvalidModsWithForbiddenModsReturnsTrue(Mods mods)
    {
        // Arrange & Act
        var result = ScoreSubmissionUtil.IsHasInvalidMods(mods);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TestIsHasInvalidModsWithAllowedModsReturnsFalse()
    {
        // Arrange & Act
        var result = ScoreSubmissionUtil.IsHasInvalidMods(Mods.Hidden | Mods.HardRock);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TestGetTimeElapsedWithPassedScoreReturnsScoreTime()
    {
        // Arrange
        var submittedScore = CreateSubmittedScore(true, Mods.None);

        // Act
        var result = ScoreSubmissionUtil.GetTimeElapsed(submittedScore, 123, 45);

        // Assert
        Assert.Equal(123, result);
    }

    [Fact]
    public void TestGetTimeElapsedWithFailedScoreReturnsFailTime()
    {
        // Arrange
        var submittedScore = CreateSubmittedScore(false, Mods.None);

        // Act
        var result = ScoreSubmissionUtil.GetTimeElapsed(submittedScore, 123, 45);

        // Assert
        Assert.Equal(45, result);
    }

    [Fact]
    public void TestGetTimeElapsedWithNoFailScoreReturnsScoreTime()
    {
        // Arrange
        var submittedScore = CreateSubmittedScore(false, Mods.NoFail);

        // Act
        var result = ScoreSubmissionUtil.GetTimeElapsed(submittedScore, 123, 45);

        // Assert
        Assert.Equal(123, result);
    }

    [Fact]
    public void TestIsScoreFailedWithFailedScoreReturnsTrue()
    {
        // Arrange
        var score = CreateScore(isPassed: false, mods: Mods.None);

        // Act
        var result = ScoreSubmissionUtil.IsScoreFailed(score);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TestIsScoreFailedWithNoFailScoreReturnsFalse()
    {
        // Arrange
        var score = CreateScore(isPassed: false, mods: Mods.NoFail);

        // Act
        var result = ScoreSubmissionUtil.IsScoreFailed(score);

        // Assert
        Assert.False(result);
    }

    private static Score CreateScore(
        int id = 55,
        long totalScore = 1000,
        int maxCombo = 400,
        double accuracy = 99,
        double performancePoints = 100,
        bool isPassed = true,
        bool isScoreable = true,
        Mods mods = Mods.None,
        SubmissionStatus submissionStatus = SubmissionStatus.Submitted,
        int? leaderboardPosition = null,
        BeatmapStatus beatmapStatus = BeatmapStatus.Ranked)
    {
        var score = new Score
        {
            Id = id,
            UserId = 77,
            BeatmapId = 11,
            BeatmapHash = "beatmap-hash",
            ScoreHash = $"score-hash-{id}",
            TotalScore = totalScore,
            MaxCombo = maxCombo,
            Count300 = 100,
            Count100 = 10,
            Count50 = 0,
            CountMiss = 0,
            CountKatu = 0,
            CountGeki = 0,
            Perfect = true,
            Mods = mods,
            Grade = "A",
            IsPassed = isPassed,
            IsScoreable = isScoreable,
            SubmissionStatus = submissionStatus,
            GameMode = GameMode.Standard,
            WhenPlayed = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            OsuVersion = "b20260101.1",
            BeatmapStatus = beatmapStatus,
            ClientTime = new DateTime(2026, 1, 2, 3, 4, 5),
            Accuracy = accuracy,
            PerformancePoints = performancePoints,
            TimeElapsed = 120
        };

        score.LocalProperties = score.LocalProperties.FromScore(score);
        score.LocalProperties.LeaderboardPosition = leaderboardPosition;
        return score;
    }

    private static UserStats CreateUserStats(
        long totalScore,
        long rankedScore,
        int maxCombo,
        double accuracy,
        double performancePoints,
        long rank)
    {
        var userStats = new UserStats
        {
            UserId = 77,
            GameMode = GameMode.Standard,
            TotalScore = totalScore,
            RankedScore = rankedScore,
            MaxCombo = maxCombo,
            Accuracy = accuracy,
            PerformancePoints = performancePoints,
            PlayCount = 1,
            PlayTime = 120,
            TotalHits = 110
        };

        userStats.LocalProperties.Rank = rank;
        return userStats;
    }

    private static SubmittedScore CreateSubmittedScore(bool isPassed, Mods mods)
    {
        return new SubmittedScore
        {
            BeatmapHash = "beatmap-hash",
            PlayerUsername = "player",
            ScoreHash = "score-hash",
            Count300 = 100,
            Count100 = 10,
            Count50 = 0,
            CountGeki = 0,
            CountKatu = 0,
            CountMiss = 0,
            TotalScore = 1000,
            MaxCombo = 300,
            Perfect = true,
            Grade = "A",
            Mods = mods,
            IsPassed = isPassed,
            GameMode = GameMode.Standard,
            WhenPlayed = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            OsuVersion = "b20260101.1",
            ClientTime = new DateTime(2026, 1, 2, 3, 4, 5),
            Accuracy = 99
        };
    }

    private static Beatmap CreateBeatmap(string statusString = "ranked")
    {
        return new Beatmap
        {
            Id = 11,
            BeatmapsetId = 22,
            DifficultyRating = 5,
            Mode = "osu",
            StatusString = statusString,
            TotalLength = 120,
            UserId = 99,
            Version = "Insane",
            BPM = 180,
            HitLength = 100,
            LastUpdated = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            ModeInt = 0,
            Passcount = 44,
            Playcount = 33,
            Ranked = statusString == "loved" ? (int)BeatmapStatus.Loved : (int)BeatmapStatus.Ranked,
            Url = "https://example/map",
            Checksum = "beatmap-hash"
        };
    }
}