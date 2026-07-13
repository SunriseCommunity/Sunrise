using osu.Shared;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Utils;
using Xunit;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Tests.Utils;

public class ScoreSubmissionValidationTests
{
    [Theory]
    [InlineData("b20240101")]
    [InlineData("20260412")]
    [InlineData("b20240101.2")]
    [InlineData("b20240101beta")]
    [InlineData("b20240101.12cuttingedge")]
    public void ClientVersionPatternAcceptsStableFormats(string version) =>
        Assert.True(OsuVersion.IsValidClientVersion(version));

    [Theory]
    [InlineData("20240101")]
    [InlineData("b202401")]
    [InlineData("b20240101.2junk")]
    [InlineData("b20241340")]
    public void ClientVersionPatternRejectsMalformedFormats(string version) =>
        Assert.False(OsuVersion.IsValidClientVersion(version));

    [Fact]
    public void ParserRejectsScoreAboveStableIntegerLimit()
    {
        var score = ValidScoreString().Replace(":1000000:100:", ":9900000000:100:");
        Assert.True(score.TryParseBaseScore(DateTime.UtcNow).IsFailure);
    }

    [Fact]
    public void ParserRejectsUnknownGrade()
    {
        var score = ValidScoreString().Replace(":X:0:", ":Z:0:");
        Assert.True(score.TryParseBaseScore(DateTime.UtcNow).IsFailure);
    }

    [Fact]
    public void StandardPerfectPlayCalculatesSilverSsWithHidden()
    {
        var score = CreateSubmittedScore(Mods.Hidden);
        Assert.Equal(ScoreGrade.XH, ScoreGradeUtil.Calculate(score));
    }

    [Fact]
    public void FailedPlayAlwaysCalculatesF()
    {
        var score = CreateSubmittedScore(isPassed: false);
        Assert.Equal(ScoreGrade.F, ScoreGradeUtil.Calculate(score));
    }

    [Fact]
    public void StandardExactEightyPercentWithMissesCalculatesB()
    {
        var score = CreateSubmittedScore(count300: 76, count100: 14, count50: 1, countMiss: 4);
        Assert.Equal(ScoreGrade.B, ScoreGradeUtil.Calculate(score));
    }

    [Theory]
    [InlineData(GameMode.CatchTheBeat, 100, ScoreGrade.X)]
    [InlineData(GameMode.CatchTheBeat, 98, ScoreGrade.A)]
    [InlineData(GameMode.CatchTheBeat, 94, ScoreGrade.B)]
    [InlineData(GameMode.CatchTheBeat, 90, ScoreGrade.C)]
    [InlineData(GameMode.CatchTheBeat, 85, ScoreGrade.D)]
    [InlineData(GameMode.Mania, 100, ScoreGrade.X)]
    [InlineData(GameMode.Mania, 95, ScoreGrade.A)]
    [InlineData(GameMode.Mania, 90, ScoreGrade.B)]
    [InlineData(GameMode.Mania, 80, ScoreGrade.C)]
    [InlineData(GameMode.Mania, 70, ScoreGrade.D)]
    public void AccuracyGradeThresholdsAreStrict(GameMode mode, double accuracy, ScoreGrade expected)
    {
        Assert.Equal(expected, ScoreGradeUtil.Calculate(CreateSubmittedScore(gameMode: mode, accuracy: accuracy)));
    }

    private static string ValidScoreString() =>
        "0123456789abcdef0123456789abcdef:player:abcdef0123456789abcdef0123456789:100:0:0:0:0:0:1000000:100:True:X:0:True:0:240101120000:b20240101";

    private static SubmittedScore CreateSubmittedScore(Mods mods = Mods.None, bool isPassed = true, int count300 = 100,
        int count100 = 0, int count50 = 0, int countMiss = 0, GameMode gameMode = GameMode.Standard, double accuracy = 100) => new()
    {
        PlayerUsername = "player", ScoreHash = "hash", BeatmapHash = "map", TotalScore = 1_000_000,
        MaxCombo = 100, Count300 = count300, Count100 = count100, Count50 = count50, CountMiss = countMiss, CountKatu = 0,
        CountGeki = 0, Perfect = true, Mods = mods, Grade = "X", IsPassed = isPassed,
        GameMode = gameMode, WhenPlayed = DateTime.UtcNow, OsuVersion = "b20240101",
        ClientTime = DateTime.UtcNow, Accuracy = accuracy
    };
}
