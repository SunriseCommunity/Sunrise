using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Tests.Abstracts;
using Mods = osu.Shared.Mods;
using PerformanceCalculatorClass = Sunrise.Shared.Utils.Calculators.PerformanceCalculator;

namespace Sunrise.Server.Tests.Utils.Calculators.PerformanceCalculator;

public class CalculateAccuracyTests : BaseTest
{
    [Fact]
    public void CalculateScoreAccuracyForOsuStandardTest()
    {
        // Arrange
        var score = new Score
        {
            Count300 = 317,
            CountGeki = 69,
            Count100 = 38,
            CountKatu = 22,
            Count50 = 2,
            CountMiss = 1,
            GameMode = GameMode.Standard
        };

        // Act
        var scoreAccuracy = PerformanceCalculatorClass.CalculateAccuracy(score);

        // Assert
        Assert.Equal(92.18, Math.Round(scoreAccuracy, 2));
    }

    [Fact]
    public void CalculateScoreAccuracyForEmptyScoreTest()
    {
        // Arrange
        var score = new Score
        {
            Count300 = 0,
            CountGeki = 0,
            Count100 = 0,
            CountKatu = 0,
            Count50 = 0,
            CountMiss = 0,
            GameMode = GameMode.Standard
        };

        // Act
        var scoreAccuracy = PerformanceCalculatorClass.CalculateAccuracy(score);

        // Assert
        Assert.Equal(0, Math.Round(scoreAccuracy, 2));
    }



    [Fact]
    public void CalculateScoreAccuracyForOsuTaikoTest()
    {
        // Arrange
        var score = new Score
        {
            Count300 = 97,
            CountGeki = 0,
            Count100 = 16,
            CountKatu = 0,
            Count50 = 0,
            CountMiss = 2,
            GameMode = GameMode.Taiko
        };

        // Act
        var scoreAccuracy = PerformanceCalculatorClass.CalculateAccuracy(score);

        // Assert
        Assert.Equal(91.30, Math.Round(scoreAccuracy, 2));
    }

    [Fact]
    public void CalculateScoreAccuracyForOsuCatchTheBeatTest()
    {
        // Arrange
        var score = new Score
        {
            Count300 = 84,
            CountGeki = 11,
            Count100 = 0,
            CountKatu = 1,
            Count50 = 155,
            CountMiss = 0,
            GameMode = GameMode.CatchTheBeat
        };

        // Act
        var scoreAccuracy = PerformanceCalculatorClass.CalculateAccuracy(score);

        // Assert
        Assert.Equal(99.58, Math.Round(scoreAccuracy, 2));
    }

    [Fact]
    public void CalculateScoreAccuracyForOsuManiaTest()
    {
        // Arrange
        var score = new Score
        {
            Count300 = 2335,
            CountGeki = 8321,
            Count100 = 19,
            CountKatu = 115,
            Count50 = 11,
            CountMiss = 27,
            GameMode = GameMode.Mania
        };

        // Act
        var scoreAccuracy = PerformanceCalculatorClass.CalculateAccuracy(score);

        // Assert
        Assert.Equal(99.19, Math.Round(scoreAccuracy, 2));
    }

    [Fact]
    public void CalculateScoreAccuracyForOsuManiaV2Test()
    {
        // Arrange
        var score = new Score
        {
            Count300 = 407,
            CountGeki = 469,
            Count100 = 61,
            CountKatu = 203,
            Count50 = 5,
            CountMiss = 29,
            GameMode = GameMode.Mania,
            Mods = Mods.ScoreV2
        };

        // Act
        var scoreAccuracy = PerformanceCalculatorClass.CalculateAccuracy(score);

        // Assert
        Assert.Equal(87.16, Math.Round(scoreAccuracy, 2));
    }
}