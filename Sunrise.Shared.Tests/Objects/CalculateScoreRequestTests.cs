using System.Text.Json;
using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Objects.Serializable.Performances;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Tests.Objects;

public class CalculateScoreRequestTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(72216504)]
    [InlineData((long)uint.MaxValue)]
    public void IncludesStableStandardScore(long totalScore)
    {
        var request = new CalculateScoreRequest(new Score
        {
            GameMode = GameMode.Standard,
            Mods = Mods.Hidden,
            TotalScore = totalScore
        });

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request));
        Assert.Equal(totalScore, json.RootElement.GetProperty("legacyTotalScore").GetInt64());
    }

    [Theory]
    [InlineData(GameMode.Taiko, Mods.None, 1000)]
    [InlineData(GameMode.CatchTheBeat, Mods.None, 1000)]
    [InlineData(GameMode.Mania, Mods.None, 1000)]
    [InlineData(GameMode.RelaxStandard, Mods.Relax, 1000)]
    [InlineData(GameMode.AutopilotStandard, Mods.Relax2, 1000)]
    [InlineData(GameMode.ScoreV2Standard, Mods.ScoreV2, 1000)]
    [InlineData(GameMode.Standard, Mods.Relax, 1000)]
    [InlineData(GameMode.Standard, Mods.Relax2, 1000)]
    [InlineData(GameMode.Standard, Mods.ScoreV2, 1000)]
    [InlineData(GameMode.Standard, Mods.None, -1)]
    [InlineData(GameMode.Standard, Mods.None, (long)uint.MaxValue + 1)]
    public void OmitsIncompatibleScore(GameMode mode, Mods mods, long totalScore)
    {
        var request = new CalculateScoreRequest(new Score
        {
            GameMode = mode,
            Mods = mods,
            TotalScore = totalScore
        });

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request));
        Assert.False(json.RootElement.TryGetProperty("legacyTotalScore", out _));
    }
}
