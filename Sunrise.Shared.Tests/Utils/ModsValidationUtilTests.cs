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
    public void TestIsModeCombinationInvalidWithForbiddenModsReturnsSuccess(Mods mods)
    {
        // Arrange & Act
        var result = ModsValidationUtil.ValidateMods(mods, GameMode.Standard);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TestIsModeCombinationInvalidWithAllowedModsReturnsFailure()
    {
        // Arrange & Act
        var result = ModsValidationUtil.ValidateMods(Mods.Hidden | Mods.HardRock, GameMode.Standard);

        // Assert
        Assert.False(result.IsFailure);
    }

    // TODO: Add more test suites
}