using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Performances;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Tests.Extensions;

public class PerformanceAttributesExtensionsTests
{

    public static GameMode[] CustomRecalculatedGameModes = [GameMode.RelaxStandard, GameMode.AutopilotStandard, GameMode.RelaxCatchTheBeat];
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes()
    {
        return Enum.GetValues(typeof(GameMode)).Cast<GameMode>().Select(mode => new object[]
        {
            mode
        });
    }

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public void IsShouldNotRecalculatePerformancesWhichDoesntSupportRecalculation(GameMode gameMode)
    {
        // Arrange
        var performance = _mocker.Score.GetRandomPerformanceAttributes();
        performance.Difficulty.Mode = (GameMode)gameMode.ToVanillaGameMode();

        var isSupportsCustomRecalculation = CustomRecalculatedGameModes.Contains(gameMode);

        var mods = gameMode.GetGamemodeMods() | Mods.Easy; // Add Easy to change RelaxCtb value;

        var oldPpValue = performance.PerformancePoints;

        // Act
        var newPp = performance.ApplyNotStandardModRecalculationsIfNeeded(_mocker.Score.GetRandomAccuracy(), mods);
        
        // Assert
        if (isSupportsCustomRecalculation)
        {
            Assert.NotEqual(newPp.PerformancePoints, oldPpValue);
        }
        else
        {
            Assert.Equal(newPp.PerformancePoints, oldPpValue);
        }
    }
    
    [Theory]
    [MemberData(nameof(GetGameModes))]
    public void IsShouldNotRecalculateScoresWhichDoesntSupportRecalculation(GameMode gameMode)
    {
        // Arrange
        var score = _mocker.Score.GetRandomScore();
        var performance = _mocker.Score.GetRandomPerformanceAttributes();
        performance.Difficulty.Mode = (GameMode)gameMode.ToVanillaGameMode();

        score.GameMode = gameMode;
        score.Mods = gameMode.GetGamemodeMods() | Mods.Easy; // Add Easy to change RelaxCtb value;

        var isSupportsCustomRecalculation = CustomRecalculatedGameModes.Contains(gameMode);

        var oldPpValue = performance.PerformancePoints;

        // Act
        var newPp = performance.ApplyNotStandardModRecalculationsIfNeeded(score);
        
        // Assert
        if (isSupportsCustomRecalculation)
        {
            Assert.NotEqual(newPp.PerformancePoints, oldPpValue);
        }
        else
        {
            Assert.Equal(newPp.PerformancePoints, oldPpValue);
        }
    }
}