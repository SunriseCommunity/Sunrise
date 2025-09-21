using osu.Shared;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Tests.Extensions;

public class UserStatsExtensionsDatabaseTests : DatabaseTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestUpdateWithScoreWithRankedScore()
    {
        // Arrange
        var user = await CreateTestUser();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.LocalProperties.IsRanked = true;
        score.PerformancePoints = 100;

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        var prevStats = userStats.Clone();

        // Act
        await userStats.UpdateWithScore(score, null, 100);

        // Assert
        var shouldIncludeKatuGeki = (GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania;
        var expectedTotalHits = prevStats.TotalHits + score.Count300 + score.Count100 + score.Count50 + (shouldIncludeKatuGeki ? score.CountGeki + score.CountKatu : 0);

        Assert.Equal(expectedTotalHits, userStats.TotalHits);
        Assert.Equal(prevStats.PlayTime + 100, userStats.PlayTime);
        Assert.Equal(prevStats.PlayCount + 1, userStats.PlayCount);
        Assert.Equal(prevStats.TotalScore + score.TotalScore, userStats.TotalScore);
        Assert.Equal(prevStats.RankedScore + score.TotalScore, userStats.RankedScore);
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);

        const double weightedTolerance = 0.5;
        Assert.True(Math.Abs(prevStats.PerformancePoints + 100 - userStats.PerformancePoints) < weightedTolerance);
        Assert.True(Math.Abs(score.Accuracy - userStats.Accuracy) < weightedTolerance);
    }

    [Fact]
    public async Task TestUpdateWithScoreWithBetterRankedScore()
    {
        // Arrange
        var user = await CreateTestUser();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.LocalProperties.IsRanked = true;
        score.PerformancePoints = 100;

        var oldScore = _mocker.Score.GetBestScoreableRandomScore();
        oldScore.TotalScore = score.TotalScore - 1;

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        var prevStats = userStats.Clone();

        // Act
        await userStats.UpdateWithScore(score, new UserPersonalBestScores(oldScore), 100);

        // Assert
        var shouldIncludeKatuGeki = (GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania;
        var expectedTotalHits = prevStats.TotalHits + score.Count300 + score.Count100 + score.Count50 + (shouldIncludeKatuGeki ? score.CountGeki + score.CountKatu : 0);

        Assert.Equal(expectedTotalHits, userStats.TotalHits);
        Assert.Equal(prevStats.PlayTime + 100, userStats.PlayTime);
        Assert.Equal(prevStats.PlayCount + 1, userStats.PlayCount);
        Assert.Equal(score.TotalScore, userStats.TotalScore);
        Assert.Equal(score.TotalScore - oldScore.TotalScore, userStats.RankedScore);
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);

        const double weightedTolerance = 0.5;
        Assert.True(Math.Abs(prevStats.PerformancePoints + 100 - userStats.PerformancePoints) < weightedTolerance);
        Assert.True(Math.Abs(score.Accuracy - userStats.Accuracy) < weightedTolerance);
    }

    [Fact]
    public async Task TestUpdateWithScoreWithBetterRankedScoreUsingNewPerformanceCalculationAlgorithmUpdateRankedScoreOnly()
    {
        // Arrange
        var user = await CreateTestUser();

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.LocalProperties.IsRanked = true;
        score.PerformancePoints = 100;

        var oldScore = _mocker.Score.GetBestScoreableRandomScore();
        oldScore.TotalScore = score.TotalScore + 1;

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        var prevStats = userStats.Clone();

        // Act
        await userStats.UpdateWithScore(score, new UserPersonalBestScores(oldScore, oldScore), 100);

        // Assert
        var shouldIncludeKatuGeki = (GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania;
        var expectedTotalHits = prevStats.TotalHits + score.Count300 + score.Count100 + score.Count50 + (shouldIncludeKatuGeki ? score.CountGeki + score.CountKatu : 0);

        Assert.Equal(expectedTotalHits, userStats.TotalHits);
        Assert.Equal(prevStats.PlayTime + 100, userStats.PlayTime);
        Assert.Equal(prevStats.PlayCount + 1, userStats.PlayCount);
        Assert.Equal(score.TotalScore, userStats.TotalScore);
        Assert.Equal(0, userStats.RankedScore); // No updates
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);

        Assert.Equal(prevStats.PerformancePoints, userStats.PerformancePoints);
        Assert.Equal(prevStats.Accuracy, userStats.Accuracy);
    }

    [Fact]
    public async Task TestUpdateWithScoreWithBetterRankedScoreUsingNewPerformanceCalculationAlgorithmUpdateOnlyPerformancePoints()
    {
        // Arrange
        var user = await CreateTestUser();

        EnvManager.Set("General:UseNewPerformanceCalculationAlgorithm", "true");
        
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.LocalProperties.IsRanked = true;
        score.PerformancePoints = 100;
        score.GameMode = GameMode.Standard;
        score.Mods = Mods.None;

        var oldScore = _mocker.Score.GetBestScoreableRandomScore();
        oldScore.TotalScore = score.TotalScore + 1;
        oldScore.PerformancePoints = score.PerformancePoints - 1;
        oldScore.GameMode = score.GameMode;
        oldScore.Mods = score.Mods;

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        var prevStats = userStats.Clone();

        // Act
        await userStats.UpdateWithScore(score, new UserPersonalBestScores(oldScore, oldScore), 100);

        // Assert
        var shouldIncludeKatuGeki = (GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania;
        var expectedTotalHits = prevStats.TotalHits + score.Count300 + score.Count100 + score.Count50 + (shouldIncludeKatuGeki ? score.CountGeki + score.CountKatu : 0);

        Assert.Equal(expectedTotalHits, userStats.TotalHits);
        Assert.Equal(prevStats.PlayTime + 100, userStats.PlayTime);
        Assert.Equal(prevStats.PlayCount + 1, userStats.PlayCount);
        Assert.Equal(score.TotalScore, userStats.TotalScore);
        Assert.Equal(0, userStats.RankedScore); // No updates
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);

        const double weightedTolerance = 0.5;
        Assert.True(Math.Abs(prevStats.PerformancePoints + 100 - userStats.PerformancePoints) < weightedTolerance);
        Assert.True(Math.Abs(score.Accuracy - userStats.Accuracy) < weightedTolerance);
    }

    [Fact]
    public async Task TestUpdateWithScoreWithWorseRankedScore()
    {
        // Arrange
        var user = await CreateTestUser();

        var oldScore = _mocker.Score.GetBestScoreableRandomScore();
        oldScore.LocalProperties.IsRanked = true;

        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.TotalScore = oldScore.TotalScore - 1;
        score.PerformancePoints = 100;

        var userStats = await Database.Users.Stats.GetUserStats(user.Id, score.GameMode);
        var prevStats = userStats.Clone();

        // Act
        await userStats.UpdateWithScore(score, new UserPersonalBestScores(oldScore), 100);

        // Assert
        var shouldIncludeKatuGeki = (GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania;
        var expectedTotalHits = prevStats.TotalHits + score.Count300 + score.Count100 + score.Count50 + (shouldIncludeKatuGeki ? score.CountGeki + score.CountKatu : 0);

        Assert.Equal(expectedTotalHits, userStats.TotalHits);
        Assert.Equal(prevStats.PlayTime + 100, userStats.PlayTime);
        Assert.Equal(prevStats.PlayCount + 1, userStats.PlayCount);
        Assert.Equal(score.TotalScore, userStats.TotalScore);
        Assert.Equal(0, userStats.RankedScore);

        const double weightedTolerance = 0.5;
        Assert.True(Math.Abs(prevStats.PerformancePoints - userStats.PerformancePoints) < weightedTolerance);
        Assert.True(Math.Abs(userStats.Accuracy - userStats.Accuracy) < weightedTolerance);
    }
}

public class UserStatsExtensionsTests : BaseTest
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Select(mode => new object[]
        {
            mode
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task TestUpdateWithScoreWithUnscoreableScore(bool isScoreScoreable, bool isScoreFailed)
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.MaxCombo = int.MaxValue;
        score.IsScoreable = isScoreScoreable;
        score.IsPassed = !isScoreFailed;
        score.LocalProperties.FromScore(score);

        var userStats = _mocker.User.GetRandomUserStats();
        userStats.MaxCombo = 0;
        userStats.GameMode = score.GameMode;

        var prevStats = userStats.Clone();

        // Act
        await userStats.UpdateWithScore(score, null, 100);

        // Assert
        var shouldIncludeKatuGeki = (GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania;
        var expectedTotalHits = prevStats.TotalHits + score.Count300 + score.Count100 + score.Count50 + (shouldIncludeKatuGeki ? score.CountGeki + score.CountKatu : 0);

        Assert.Equal(expectedTotalHits, userStats.TotalHits);
        Assert.Equal(prevStats.PlayTime + 100, userStats.PlayTime);
        Assert.Equal(prevStats.PlayCount + 1, userStats.PlayCount);
        Assert.Equal(prevStats.TotalScore + score.TotalScore, userStats.TotalScore);

        var shouldUpdateMaxCombo = isScoreScoreable && !isScoreFailed;
        Assert.Equal(shouldUpdateMaxCombo ? score.MaxCombo : prevStats.MaxCombo, userStats.MaxCombo);

        Assert.Equal(prevStats.RankedScore, userStats.RankedScore);
        Assert.Equal(prevStats.PerformancePoints, userStats.PerformancePoints);
        Assert.Equal(prevStats.Accuracy, userStats.Accuracy);
    }

    [Fact]
    public async Task TestUpdateWithScoreWithWorseNewScore()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.LocalProperties.IsRanked = true;
        score.MaxCombo = int.MaxValue;
        score.TotalScore = 0;

        var oldScore = score;
        oldScore.TotalScore += 1;

        var userStats = _mocker.User.GetRandomUserStats();
        userStats.MaxCombo = 0;
        userStats.GameMode = score.GameMode;

        var prevStats = userStats.Clone();

        // Act
        await userStats.UpdateWithScore(score, new UserPersonalBestScores(oldScore), 100);

        // Assert
        var shouldIncludeKatuGeki = (GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania;
        var expectedTotalHits = prevStats.TotalHits + score.Count300 + score.Count100 + score.Count50 + (shouldIncludeKatuGeki ? score.CountGeki + score.CountKatu : 0);

        Assert.Equal(expectedTotalHits, userStats.TotalHits);
        Assert.Equal(prevStats.PlayTime + 100, userStats.PlayTime);
        Assert.Equal(prevStats.PlayCount + 1, userStats.PlayCount);
        Assert.Equal(prevStats.TotalScore + score.TotalScore, userStats.TotalScore);

        Assert.Equal(score.MaxCombo, userStats.MaxCombo);

        Assert.Equal(prevStats.RankedScore, userStats.RankedScore);
        Assert.Equal(prevStats.PerformancePoints, userStats.PerformancePoints);
        Assert.Equal(prevStats.Accuracy, userStats.Accuracy);
    }

    /// <summary>
    ///     Happens if we submitted score on a loved beatmap. It is not ranked, but it is scoreable.
    /// </summary>
    [Fact]
    public async Task TestUpdateWithScoreShouldUpdateMaxComboIfScoreScoreable()
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.LocalProperties.IsRanked = false;
        score.MaxCombo = int.MaxValue;

        var userStats = _mocker.User.GetRandomUserStats();
        userStats.GameMode = score.GameMode;
        userStats.MaxCombo = 0;

        // Act
        await userStats.UpdateWithScore(score, null, 100);

        // Assert
        Assert.Equal(score.MaxCombo, userStats.MaxCombo);
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestUpdateWithScoreUpdatesTotalHits(GameMode mode)
    {
        // Arrange
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.GameMode = mode;
        score.IsScoreable = false;

        score.Count50 = 1;
        score.Count100 = 1;
        score.Count300 = 1;
        score.CountGeki = 1;
        score.CountKatu = 1;

        var userStats = _mocker.User.GetRandomUserStats();
        userStats.GameMode = mode;

        var prevStats = userStats.Clone();

        // Act
        await userStats.UpdateWithScore(score, null, 100);

        // Assert
        var shouldIncludeKatuGeki = (GameMode)mode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania;
        var expectedTotalHits = shouldIncludeKatuGeki ? 5 : 3;

        Assert.Equal(prevStats.TotalHits + expectedTotalHits, userStats.TotalHits);
    }
}