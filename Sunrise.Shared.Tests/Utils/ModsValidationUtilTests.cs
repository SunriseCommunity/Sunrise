using osu.Shared;
using Sunrise.Processing.Utils;
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
    public void TestIsModeCombinationInvalidWithForbiddenModsReturnsTrue(Mods mods)
    {
        // Arrange & Act
        var result = ModsValidationUtil.IsModeCombinationInvalid(mods, GameMode.Standard);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TestIsModeCombinationInvalidWithAllowedModsReturnsFalse()
    {
        // Arrange & Act
        var result = ModsValidationUtil.IsModeCombinationInvalid(Mods.Hidden | Mods.HardRock, GameMode.Standard);

        // Assert
        Assert.False(result);
    }

    // TODO: Add more test suites
}