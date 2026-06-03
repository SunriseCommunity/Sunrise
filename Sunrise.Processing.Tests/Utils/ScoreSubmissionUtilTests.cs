using Sunrise.Processing.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Utils.Converters;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
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
        var score = _mocker.Score.GetBestScoreableRandomScore();

        score.IsPassed = false;
        score.Mods = Mods.None;

        // Act
        score.UpdateSubmissionStatus(null);

        // Assert
        Assert.Equal(SubmissionStatus.Failed, score.SubmissionStatus);
    }

    [Fact]
    public void TestUpdateSubmissionStatusWithUnscoreableScoreReturnsSubmittedStatus()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();

        score.IsScoreable = false;
        score.BeatmapStatus = BeatmapStatus.Pending;

        // Act
        score.UpdateSubmissionStatus(null);

        // Assert
        Assert.Equal(SubmissionStatus.Submitted, score.SubmissionStatus);
    }

    [Fact]
    public void TestUpdateSubmissionStatusWithFirstScoreReturnsBestStatus()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();

        // Act
        score.UpdateSubmissionStatus(null);

        // Assert
        Assert.Equal(SubmissionStatus.Best, score.SubmissionStatus);
    }

    [Fact]
    public void TestUpdateSubmissionStatusWithWorseScoreReturnsSubmittedStatus()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.Standard;
        score.Mods = Mods.None;
        score.TotalScore = 500;

        var previousBest = _mocker.Score.GetBestScoreableRandomScore();
        previousBest.GameMode = GameMode.Standard;
        previousBest.Mods = Mods.None;
        previousBest.TotalScore = 1000;

        // Act
        score.UpdateSubmissionStatus(previousBest);

        // Assert
        Assert.Equal(SubmissionStatus.Submitted, score.SubmissionStatus);
    }

    [Fact]
    public void TestUpdateSubmissionStatusWithWorsePerformanceForSpecialGameModesReturnsSubmittedStatus()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = GameMode.RelaxStandard;
        score.Mods = Mods.Relax;
        score.PerformancePoints = 500;
        score.TotalScore = 1000;

        var previousBest = _mocker.Score.GetBestScoreableRandomScore();
        previousBest.GameMode = GameMode.RelaxStandard;
        previousBest.Mods = Mods.Relax;
        previousBest.PerformancePoints = 1000;
        previousBest.TotalScore = 500;

        // Act
        score.UpdateSubmissionStatus(previousBest);

        // Assert
        Assert.Equal(SubmissionStatus.Submitted, score.SubmissionStatus);
    }

    [Fact]
    public void TestGetScoreSubmitResponseWithRankedBeatmapReturnsExpectedResponse()
    {
        // Arrange
        var user = _mocker.User.GetRandomUser();
        user.Id = 1;

        var newScore = _mocker.Score.GetBestScoreableRandomScore();
        newScore.EnrichWithUserData(user);
        newScore.PerformancePoints = 200;
        newScore.LocalProperties.LeaderboardPosition = 1;

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.IgnoreBeatmapRanking();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(newScore);

        var previousBeatmapBest = _mocker.Score.GetBestScoreableRandomScore();
        previousBeatmapBest.EnrichWithBeatmapData(beatmap);
        previousBeatmapBest.EnrichWithUserData(user);
        previousBeatmapBest.PerformancePoints = 50;
        previousBeatmapBest.LocalProperties.LeaderboardPosition = 5;

        var previousPerformanceBest = _mocker.Score.GetBestScoreableRandomScore();
        previousPerformanceBest.EnrichWithBeatmapData(beatmap);
        previousPerformanceBest.EnrichWithUserData(user);
        previousPerformanceBest.PerformancePoints = 100;
        previousPerformanceBest.LocalProperties.LeaderboardPosition = 6;

        var userStats = _mocker.User.GetRandomUserStats();
        userStats.EnrichWithUserData(user);


        var prevUserStats = _mocker.User.GetRandomUserStats();
        prevUserStats.EnrichWithUserData(user);

        var previousPersonalBestScores = new UserPersonalBestScores(previousBeatmapBest, previousPerformanceBest);

        var newAchievements = "new-achievements";

        var expectedResponse =
            $"beatmapId:{beatmap.Id}|beatmapSetId:{beatmap.BeatmapsetId}|beatmapPlaycount:{beatmap.Playcount}|beatmapPasscount:{beatmap.Passcount}|approvedDate:{beatmap.LastUpdated:yyyy-MM-dd}\n" +
            $"chartId:beatmap|chartUrl:{beatmap.Url}|chartName:Beatmap Ranking|rankBefore:{previousBeatmapBest.LocalProperties.LeaderboardPosition}|rankAfter:{newScore.LocalProperties.LeaderboardPosition}|rankedScoreBefore:{previousBeatmapBest.TotalScore}|rankedScoreAfter:{newScore.TotalScore}|totalScoreBefore:{previousBeatmapBest.TotalScore}|totalScoreAfter:{newScore.TotalScore}|maxComboBefore:{previousBeatmapBest.MaxCombo}|maxComboAfter:{newScore.MaxCombo}|accuracyBefore:{previousBeatmapBest.Accuracy}|accuracyAfter:{newScore.Accuracy}|ppBefore:{previousBeatmapBest.PerformancePoints}|ppAfter:{newScore.PerformancePoints}|onlineScoreId:{newScore.Id}\n" +
            $"chartId:overall|chartUrl:https://{Configuration.Domain}/user/{user.Id}|chartName:Overall Ranking|rankBefore:{prevUserStats.LocalProperties.Rank}|rankAfter:{userStats.LocalProperties.Rank}|rankedScoreBefore:{prevUserStats.RankedScore}|rankedScoreAfter:{userStats.RankedScore}|totalScoreBefore:{prevUserStats.TotalScore}|totalScoreAfter:{userStats.TotalScore}|maxComboBefore:{prevUserStats.MaxCombo}|maxComboAfter:{userStats.MaxCombo}|accuracyBefore:{prevUserStats.Accuracy}|accuracyAfter:{userStats.Accuracy}|ppBefore:{prevUserStats.PerformancePoints}|ppAfter:{userStats.PerformancePoints}|achievements-new:{newAchievements}";

        // Act
        var result = ScoreSubmissionUtil.GetScoreSubmitResponse(beatmap, userStats, prevUserStats, newScore, previousPersonalBestScores, newAchievements);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public void TestGetScoreSubmitResponseWithLovedBeatmapHidesBeatmapPpValues()
    {
        // Arrange
        var user = _mocker.User.GetRandomUser();
        user.Id = 1;

        var newScore = _mocker.Score.GetBestScoreableRandomScore();
        newScore.EnrichWithUserData(user);
        newScore.PerformancePoints = 200;
        newScore.LocalProperties.LeaderboardPosition = 1;

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();

        beatmapSet.Ranked = (int)BeatmapStatus.Loved;
        beatmapSet.StatusString = "loved";

        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(newScore);

        beatmap.Ranked = (int)BeatmapStatus.Loved;
        beatmap.StatusString = "loved";

        var previousBeatmapBest = _mocker.Score.GetBestScoreableRandomScore();
        previousBeatmapBest.EnrichWithBeatmapData(beatmap);
        previousBeatmapBest.EnrichWithUserData(user);
        previousBeatmapBest.PerformancePoints = 50;
        previousBeatmapBest.LocalProperties.LeaderboardPosition = 5;

        var previousPerformanceBest = _mocker.Score.GetBestScoreableRandomScore();
        previousPerformanceBest.EnrichWithBeatmapData(beatmap);
        previousPerformanceBest.EnrichWithUserData(user);
        previousPerformanceBest.PerformancePoints = 100;
        previousPerformanceBest.LocalProperties.LeaderboardPosition = 6;

        var userStats = _mocker.User.GetRandomUserStats();
        userStats.EnrichWithUserData(user);


        var prevUserStats = _mocker.User.GetRandomUserStats();
        prevUserStats.EnrichWithUserData(user);

        var previousPersonalBestScores = new UserPersonalBestScores(previousBeatmapBest, previousPerformanceBest);

        var newAchievements = "new-achievements";

        var expectedPerformancePoints = "";

        var expectedResponse =
            $"beatmapId:{beatmap.Id}|beatmapSetId:{beatmap.BeatmapsetId}|beatmapPlaycount:{beatmap.Playcount}|beatmapPasscount:{beatmap.Passcount}|approvedDate:{beatmap.LastUpdated:yyyy-MM-dd}\n" +
            $"chartId:beatmap|chartUrl:{beatmap.Url}|chartName:Beatmap Ranking|rankBefore:{previousBeatmapBest.LocalProperties.LeaderboardPosition}|rankAfter:{newScore.LocalProperties.LeaderboardPosition}|rankedScoreBefore:{previousBeatmapBest.TotalScore}|rankedScoreAfter:{newScore.TotalScore}|totalScoreBefore:{previousBeatmapBest.TotalScore}|totalScoreAfter:{newScore.TotalScore}|maxComboBefore:{previousBeatmapBest.MaxCombo}|maxComboAfter:{newScore.MaxCombo}|accuracyBefore:{previousBeatmapBest.Accuracy}|accuracyAfter:{newScore.Accuracy}|ppBefore:{expectedPerformancePoints}|ppAfter:{expectedPerformancePoints}|onlineScoreId:{newScore.Id}\n" +
            $"chartId:overall|chartUrl:https://{Configuration.Domain}/user/{user.Id}|chartName:Overall Ranking|rankBefore:{prevUserStats.LocalProperties.Rank}|rankAfter:{userStats.LocalProperties.Rank}|rankedScoreBefore:{prevUserStats.RankedScore}|rankedScoreAfter:{userStats.RankedScore}|totalScoreBefore:{prevUserStats.TotalScore}|totalScoreAfter:{userStats.TotalScore}|maxComboBefore:{prevUserStats.MaxCombo}|maxComboAfter:{userStats.MaxCombo}|accuracyBefore:{prevUserStats.Accuracy}|accuracyAfter:{userStats.Accuracy}|ppBefore:{prevUserStats.PerformancePoints}|ppAfter:{userStats.PerformancePoints}|achievements-new:{newAchievements}";

        // Act
        var result = ScoreSubmissionUtil.GetScoreSubmitResponse(beatmap, userStats, prevUserStats, newScore, previousPersonalBestScores, newAchievements);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public void TestGetTimeElapsedWithPassedScoreReturnsScoreTime()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.IsPassed = true;

        var submittedScore = _mocker.Score.GetRandomSubmittedScore(score);

        var scoreTime = _mocker.GetRandomInteger();
        var scoreFailTime = _mocker.GetRandomInteger();

        // Act
        var result = ScoreSubmissionUtil.GetTimeElapsed(submittedScore, scoreTime, scoreFailTime);

        // Assert
        Assert.Equal(scoreTime, result);
    }

    [Fact]
    public void TestGetTimeElapsedWithFailedScoreReturnsFailTime()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.Mods = Mods.None;
        score.IsPassed = false;

        var submittedScore = _mocker.Score.GetRandomSubmittedScore(score);

        var scoreTime = _mocker.GetRandomInteger();
        var scoreFailTime = _mocker.GetRandomInteger();

        // Act
        var result = ScoreSubmissionUtil.GetTimeElapsed(submittedScore, scoreTime, scoreFailTime);

        // Assert
        Assert.Equal(scoreFailTime, result);
    }

    [Fact]
    public void TestGetTimeElapsedWithNoFailScoreReturnsScoreTime()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.Mods = Mods.NoFail;
        score.IsPassed = false;

        var submittedScore = _mocker.Score.GetRandomSubmittedScore(score);

        var scoreTime = _mocker.GetRandomInteger();
        var scoreFailTime = _mocker.GetRandomInteger();

        // Act
        var result = ScoreSubmissionUtil.GetTimeElapsed(submittedScore, scoreTime, scoreFailTime);

        // Assert
        Assert.Equal(scoreTime, result);
    }

    [Fact]
    public void TestIsScoreFailedWithFailedScoreReturnsTrue()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.Mods = Mods.None;
        score.IsPassed = false;

        // Act
        var result = ScoreSubmissionUtil.IsScoreFailed(score);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TestIsScoreFailedWithNoFailScoreReturnsFalse()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.Mods = Mods.NoFail;
        score.IsPassed = false;

        // Act
        var result = ScoreSubmissionUtil.IsScoreFailed(score);

        // Assert
        Assert.False(result);
    }
}