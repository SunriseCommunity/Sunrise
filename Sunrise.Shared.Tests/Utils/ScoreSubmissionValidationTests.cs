using osu.Shared;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Tests.Utils;

public class ScoreSubmissionValidationTests
{
    [Fact]
    public void ParserAcceptsSerializedScoreVersionAsCalendarDate()
    {
        var result = ValidScoreString().TryParseBaseScore(DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal("20260412", result.Value.OsuVersion);
    }

    [Theory]
    [InlineData("b20260412")]
    [InlineData("20260412.1")]
    [InlineData("20260412beta")]
    [InlineData("20260412cuttingedge")]
    [InlineData("20260229")]
    [InlineData("20261301")]
    [InlineData("2026041")]
    public void ParserRejectsMalformedSerializedScoreVersion(string version)
    {
        var score = ReplaceField(ValidScoreString(), 17, version);

        Assert.True(score.TryParseBaseScore(DateTime.UtcNow).IsFailure);
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("0")]
    [InlineData("x")]
    public void ParserRejectsUnknownGrade(string grade)
    {
        var score = ReplaceField(ValidScoreString(), 12, grade);
        Assert.True(score.TryParseBaseScore(DateTime.UtcNow).IsFailure);
    }

    [Theory]
    [InlineData(3, "-1")]
    [InlineData(3, "65536")]
    [InlineData(4, "-1")]
    [InlineData(4, "65536")]
    [InlineData(5, "-1")]
    [InlineData(5, "65536")]
    [InlineData(6, "-1")]
    [InlineData(6, "65536")]
    [InlineData(7, "-1")]
    [InlineData(7, "65536")]
    [InlineData(8, "-1")]
    [InlineData(8, "65536")]
    [InlineData(10, "-1")]
    [InlineData(10, "65536")]
    [InlineData(9, "-1")]
    [InlineData(9, "2147483648")]
    public void ParserRejectsValuesOutsideStableNumericRanges(int field, string value)
    {
        var score = ReplaceField(ValidScoreString(), field, value);

        Assert.True(score.TryParseBaseScore(DateTime.UtcNow).IsFailure);
    }

    [Theory]
    [InlineData("XH")]
    [InlineData("X")]
    [InlineData("SH")]
    [InlineData("S")]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    [InlineData("D")]
    [InlineData("F")]
    public void GradeRoundTripsAsCanonicalToken(string token)
    {
        var parsed = ReplaceField(ValidScoreString(), 12, token).TryParseBaseScore(DateTime.UtcNow);
        var persisted = new Sunrise.Shared.Database.Models.Score { Grade = parsed.Value.Grade };

        Assert.True(parsed.IsSuccess);
        Assert.Equal(token, parsed.Value.Grade.ToString());
        Assert.Equal(token, persisted.ToScoreString("player").Split(':')[12]);
    }

    [Theory]
    [InlineData(0, Mods.Relax, GameMode.RelaxStandard)]
    [InlineData(1, Mods.ScoreV2, GameMode.ScoreV2Taiko)]
    public void ParserTransformsVanillaWireModeUsingMods(int wireMode, Mods mods, GameMode expected)
    {
        var score = ReplaceField(ReplaceField(ValidScoreString(), 13, ((int)mods).ToString()), 15, wireMode.ToString());

        var parsed = score.TryParseBaseScore(DateTime.UtcNow);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(expected, parsed.Value.GameMode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("4")]
    [InlineData("12")]
    public void ParserRejectsGameModeOutsideVanillaWireRange(string wireMode)
    {
        var score = ReplaceField(ValidScoreString(), 15, wireMode);

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

    private static string ValidScoreString()
    {
        return "0123456789abcdef0123456789abcdef:player:abcdef0123456789abcdef0123456789:100:0:0:0:0:0:1000000:100:True:X:0:True:0:240101120000:20260412";
    }

    private static string ReplaceField(string score, int index, string value)
    {
        var fields = score.Split(':');
        fields[index] = value;
        return string.Join(':', fields);
    }

    private static SubmittedScore CreateSubmittedScore(Mods mods = Mods.None, bool isPassed = true, int count300 = 100,
        int count100 = 0, int count50 = 0, int countMiss = 0, GameMode gameMode = GameMode.Standard, double accuracy = 100)
    {
        return new SubmittedScore
        {
            PlayerUsername = "player",
            ScoreHash = "hash",
            BeatmapHash = "map",
            TotalScore = 1_000_000,
            MaxCombo = 100,
            Count300 = count300,
            Count100 = count100,
            Count50 = count50,
            CountMiss = countMiss,
            CountKatu = 0,
            CountGeki = 0,
            Perfect = true,
            Mods = mods,
            Grade = ScoreGrade.X,
            IsPassed = isPassed,
            GameMode = gameMode,
            WhenPlayed = DateTime.UtcNow,
            OsuVersion = "20260412",
            ClientTime = DateTime.UtcNow,
            Accuracy = accuracy
        };
    }
}
