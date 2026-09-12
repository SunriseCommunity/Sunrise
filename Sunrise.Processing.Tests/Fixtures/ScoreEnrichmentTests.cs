using osu.Shared;
using Sunrise.Processing.Utils;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Utils;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using InternalGameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Processing.Tests.Fixtures;

public class ScoreEnrichmentTests
{
    [Fact]
    public void DefaultBeatmapEnrichmentCreatesAdmissionValidNativeStandardPassedScore()
    {
        var mock = new MockService();
        var score = mock.Score.GetRandomScore();
        score.GameMode = InternalGameMode.ScoreV2Mania;
        score.Mods = Mods.ScoreV2 | Mods.Key4 | Mods.Mirror;
        score.IsPassed = true;
        score.Count300 = 70_000;
        score.Count100 = 70_000;
        score.Count50 = 70_000;
        score.CountGeki = 10;
        score.CountKatu = 10;
        score.CountMiss = 1;
        score.MaxCombo = 70_000;
        score.Perfect = true;
        score.Grade = ScoreGrade.F;
        var beatmap = new Beatmap
        {
            Id = 123,
            Checksum = "beatmap-checksum",
            StatusString = "ranked",
            Mode = "osu",
            ModeInt = (int)osu.Shared.GameMode.Standard,
            Convert = false,
            CountCircles = 40,
            CountSliders = 50,
            CountSpinners = 10,
            MaxCombo = 150,
            Version = "fixture"
        };

        score.PrepareForSubmission(beatmap);
        var submittedScore = mock.Score.GetRandomSubmittedScore(score);

        Assert.True(ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap).IsSuccess);
        Assert.True(ModsValidationUtil.ValidateMods(score.Mods, score.GameMode.ToVanillaGameMode()).IsSuccess);
        ScoreCandidateBuilderUtil.AssertGrade(score, submittedScore);
        Assert.Equal(ScoreGrade.X, score.Grade);
        Assert.Equal(InternalGameMode.ScoreV2Standard, score.GameMode);
        Assert.Equal(0, score.CountGeki);
        Assert.Equal(0, score.CountKatu);
    }

    [Fact]
    public void BeatmapEnrichmentOptOutPreservesIntentionalSubmissionInconsistencies()
    {
        var mock = new MockService();
        var score = mock.Score.GetRandomScore();
        score.IsPassed = true;
        score.Count300 = 70_000;
        score.CountMiss = 10;
        score.MaxCombo = 80_000;
        score.Perfect = true;
        score.Grade = ScoreGrade.F;
        var beatmap = new Beatmap
        {
            Id = 456,
            Checksum = "exception-beatmap",
            StatusString = "ranked",
            Mode = "osu",
            ModeInt = (int)osu.Shared.GameMode.Standard,
            Convert = false,
            CountCircles = 10,
            CountSliders = 10,
            CountSpinners = 10,
            MaxCombo = 40,
            Version = "fixture"
        };

        score.EnrichWithBeatmapData(beatmap);
        score.ReconcileModsAndGameMode(beatmap);

        Assert.Equal(beatmap.Id, score.BeatmapId);
        Assert.Equal(70_000, score.Count300);
        Assert.Equal(10, score.CountMiss);
        Assert.Equal(80_000, score.MaxCombo);
        Assert.True(score.Perfect);
        Assert.Equal(ScoreGrade.F, score.Grade);
        Assert.True(ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap).IsFailure);
    }
}
