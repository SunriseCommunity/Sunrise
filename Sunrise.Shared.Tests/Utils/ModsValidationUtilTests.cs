using osu.Shared;
using Sunrise.Shared.Utils;
using Sunrise.Tests.Abstracts;
using Mods = osu.Shared.Mods;

namespace Sunrise.Shared.Tests.Utils;

public class ModsValidationUtilTests : BaseTest
{
    [Theory]
    [InlineData(Mods.Target)]
    [InlineData(Mods.Random)]
    [InlineData(Mods.KeyCoop)]
    [InlineData(Mods.Cinema)]
    [InlineData(Mods.Autoplay)]
    public void TestValidateModsWithForbiddenModsReturnsFailure(Mods mods)
    {
        // Arrange & Act
        var result = ModsValidationUtil.ValidateMods(mods, GameMode.Standard);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TestValidateModsWithAllowedModsReturnsSuccess()
    {
        // Arrange & Act
        var result = ModsValidationUtil.ValidateMods(Mods.Hidden | Mods.HardRock, GameMode.Standard);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(GameMode.Standard, Mods.SpunOut)]
    [InlineData(GameMode.Standard, Mods.DoubleTime | Mods.Nightcore)]
    [InlineData(GameMode.Standard, Mods.SuddenDeath | Mods.Perfect)]
    [InlineData(GameMode.Mania, Mods.Key4)]
    [InlineData(GameMode.Mania, Mods.FadeIn)]
    [InlineData(GameMode.Mania, Mods.Mirror)]
    [InlineData(GameMode.Taiko, Mods.Hidden | Mods.HardRock)]
    [InlineData(GameMode.CatchTheBeat, Mods.DoubleTime | Mods.Flashlight)]
    public void TestValidateModsWithGameModeAllowedModsReturnsSuccess(GameMode gameMode, Mods mods)
    {
        // Arrange & Act
        var result = ModsValidationUtil.ValidateMods(mods, gameMode);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(GameMode.Standard, Mods.Key4)]
    [InlineData(GameMode.Standard, Mods.FadeIn)]
    [InlineData(GameMode.Taiko, Mods.SpunOut)]
    [InlineData(GameMode.CatchTheBeat, Mods.Mirror)]
    public void TestValidateModsWithGameModeForbiddenModsReturnsFailure(GameMode gameMode, Mods mods)
    {
        // Arrange & Act
        var result = ModsValidationUtil.ValidateMods(mods, gameMode);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(GameMode.Standard, Mods.None)]
    [InlineData(GameMode.Standard, Mods.TouchDevice)]
    [InlineData(GameMode.Standard, Mods.ScoreV2)]
    [InlineData(GameMode.Standard, Mods.TouchDevice | Mods.ScoreV2)]
    public void TestValidateModsWithIgnoredModsReturnsSuccess(GameMode gameMode, Mods mods)
    {
        // Arrange & Act
        var result = ModsValidationUtil.ValidateMods(mods, gameMode);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(GameMode.Standard, Mods.DoubleTime | Mods.HalfTime)]
    [InlineData(GameMode.Standard, Mods.NoFail | Mods.SuddenDeath)]
    [InlineData(GameMode.Standard, Mods.Easy | Mods.HardRock)]
    [InlineData(GameMode.Mania, Mods.Key1 | Mods.Key2)]
    [InlineData(GameMode.Standard, Mods.Relax | Mods.ScoreV2)]
    public void TestValidateModsWithSingleInstanceConflictsReturnsFailure(GameMode gameMode, Mods mods)
    {
        // Arrange & Act
        var result = ModsValidationUtil.ValidateMods(mods, gameMode);

        // Assert
        Assert.True(result.IsFailure);
    }
}