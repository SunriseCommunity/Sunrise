using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Utils;
using Sunrise.Tests.Services.Mock;
using InternalGameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Tests.Services.Mock;

public class MockScoreServiceTests
{
    [Fact]
    public void GeneratedScoresPreserveSubmissionFieldsAcrossWireRoundtrip()
    {
        var mock = new MockService();

        for (var i = 0; i < 100; i++)
        {
            var score = mock.Score.GetValidScore(mock.Beatmap.GetRandomBeatmap());

            var parsed = score.ToScoreString("fixture-user").TryParseBaseScore(score.WhenPlayed);

            Assert.True(parsed.IsSuccess, parsed.IsFailure ? parsed.Error : null);
            Assert.Equal(score.GameMode, parsed.Value.GameMode);
            Assert.Equal(score.Mods, parsed.Value.Mods);
            Assert.Equal(score.Count300, parsed.Value.Count300);
            Assert.Equal(score.Count100, parsed.Value.Count100);
            Assert.Equal(score.Count50, parsed.Value.Count50);
            Assert.Equal(score.CountGeki, parsed.Value.CountGeki);
            Assert.Equal(score.CountKatu, parsed.Value.CountKatu);
            Assert.Equal(score.CountMiss, parsed.Value.CountMiss);
            Assert.Equal(score.Grade, parsed.Value.Grade);
        }
    }

    [Fact]
    public void GeneratedModsAreImmediatelyValidAndPreserveEveryInternalGameMode()
    {
        var mock = new MockService();

        foreach (var gameMode in Enum.GetValues<InternalGameMode>())
        {
            for (var i = 0; i < 25; i++)
            {
                var mods = mock.Score.GetRandomMods(gameMode);
                var vanillaMode = gameMode.ToVanillaGameMode();

                Assert.True(ModsValidationUtil.ValidateMods(mods, vanillaMode).IsSuccess);
                Assert.Equal(gameMode, ((InternalGameMode)vanillaMode).EnrichWithMods(mods));
            }
        }
    }
}
