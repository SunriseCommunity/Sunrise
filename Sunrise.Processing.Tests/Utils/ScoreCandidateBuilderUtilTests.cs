using Sunrise.Processing.Utils;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using Mods = osu.Shared.Mods;

namespace Sunrise.Processing.Tests.Utils;

public class ScoreCandidateBuilderUtilTests : BaseTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public void TestBuildWithValidQueueEntryReturnsScoreAndSubmittedScore()
    {
        // Arrange
        var (queueEntry, originalScore, beatmap, username, _) = CreateValidQueueEntry();

        // Act
        var result = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Assert
        Assert.True(result.IsSuccess);

        Assert.Equal(username, result.Value.submittedScore.PlayerUsername);
        Assert.Equal(queueEntry.WhenPlayed, result.Value.submittedScore.WhenPlayed);
        Assert.Equal(queueEntry.UserId, result.Value.score.UserId);
        Assert.Equal(originalScore.BeatmapHash, result.Value.score.BeatmapHash);
        Assert.Equal(originalScore.ScoreHash, result.Value.score.ScoreHash);
        Assert.Equal(beatmap.Id, result.Value.score.BeatmapId);
        Assert.Equal(queueEntry.ReplayFileId, result.Value.score.ReplayFileId);
    }

    [Fact]
    public void TestBuildWithInvalidScoreStringReturnsParsedScoreInvalidError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();

        queueEntry.ScoreSerialized = "invalid-score-string";

        // Act
        var result = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(ScoreProcessingErrorCode.ParsedScoreInvalid, result.Error.Code);
    }

    [Fact]
    public void TestValidateBuiltScoreWithValidQueueEntryReturnsSuccess()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("b20260412", "b20260412")]
    [InlineData("20260412.1", "20260412.1")]
    [InlineData("20260412", "20260413")]
    [InlineData("20260412", null)]
    public void TestValidateBuiltScoreWithInvalidOrMismatchedSubmissionVersionsReturnsInvalidClientVersion(
        string scoreVersion, string? formVersion)
    {
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);
        buildResult.Value.score.OsuVersion = scoreVersion;
        queueEntry.OsuVersion = formVersion!;

        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(
            queueEntry,
            buildResult.Value.score,
            buildResult.Value.submittedScore,
            beatmap);

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidClientVersion, result.Error.Code);
    }

    [Fact]
    public void TestValidateBuiltScoreWithInvalidGradeReturnsSuccess()
    {
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);
        buildResult.Value.score.Grade = ScoreGrade.D;
        buildResult.Value.score.ScoreHash = buildResult.Value.score.ComputeOnlineHash(
            buildResult.Value.submittedScore.PlayerUsername.Trim(), queueEntry.ClientHash, queueEntry.StoryboardHash);

        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TestValidateBuiltScoreWithValidNativeStandardScoreReturnsSuccess()
    {
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);
        beatmap.Convert = false;
        beatmap.ModeInt = (int)Sunrise.Shared.Enums.Beatmaps.GameMode.Standard;
        beatmap.CountCircles = 40;
        beatmap.CountSliders = 50;
        beatmap.CountSpinners = 10;
        beatmap.MaxCombo = 150;

        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TestValidateBuiltScoreWithPassedNativeStandardJudgmentMismatchReturnsInvalidScoreStateFirst()
    {
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(replayFileId: null);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);
        beatmap.Convert = false;
        beatmap.ModeInt = (int)Sunrise.Shared.Enums.Beatmaps.GameMode.Standard;
        beatmap.CountCircles = 40;
        beatmap.CountSliders = 50;
        beatmap.CountSpinners = 11;

        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
        Assert.Equal(ScoreProcessingDisposition.Permanent, result.Error.Disposition);
    }

    [Fact]
    public void TestAssertScoreStateAllowsStandardSliderComboAboveJudgmentCount()
    {
        var (_, score, beatmap, _, _) = CreateValidQueueEntry();
        beatmap.Convert = false;
        beatmap.ModeInt = (int)Sunrise.Shared.Enums.Beatmaps.GameMode.Standard;
        beatmap.CountCircles = 40;
        beatmap.CountSliders = 50;
        beatmap.CountSpinners = 10;
        score.MaxCombo = 200;
        beatmap.MaxCombo = 200;

        Assert.True(ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap).IsSuccess);
    }

    [Fact]
    public void TestAssertScoreStateAllowsFailedNativeStandardJudgmentPrefix()
    {
        var (_, score, beatmap, _, _) = CreateValidQueueEntry();
        score.IsPassed = false;
        score.Perfect = false;
        beatmap.Convert = false;
        beatmap.ModeInt = (int)Sunrise.Shared.Enums.Beatmaps.GameMode.Standard;
        beatmap.CountCircles = 40;
        beatmap.CountSliders = 50;
        beatmap.CountSpinners = 11;

        Assert.True(ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap).IsSuccess);
    }

    [Fact]
    public void TestAssertScoreStateRejectsFailedNativeStandardJudgmentExcess()
    {
        var (_, score, beatmap, _, _) = CreateValidQueueEntry();
        score.IsPassed = false;
        score.Perfect = false;
        beatmap.Convert = false;
        beatmap.ModeInt = (int)Sunrise.Shared.Enums.Beatmaps.GameMode.Standard;
        beatmap.CountCircles = 40;
        beatmap.CountSliders = 50;
        beatmap.CountSpinners = 9;

        var result = ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap);

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
    }

    [Fact]
    public void TestAssertScoreStateRejectsNativeStandardComboAboveMetadataMaximum()
    {
        var (_, score, beatmap, _, _) = CreateValidQueueEntry();
        beatmap.Convert = false;
        beatmap.ModeInt = (int)Sunrise.Shared.Enums.Beatmaps.GameMode.Standard;
        beatmap.CountCircles = 40;
        beatmap.CountSliders = 50;
        beatmap.CountSpinners = 10;
        beatmap.MaxCombo = 99;

        var result = ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap);

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
    }

    [Fact]
    public void TestAssertScoreStateAllowsNativeStandardComboWhenMetadataMaximumIsUnavailable()
    {
        var (_, score, beatmap, _, _) = CreateValidQueueEntry();
        beatmap.Convert = false;
        beatmap.ModeInt = (int)Sunrise.Shared.Enums.Beatmaps.GameMode.Standard;
        beatmap.CountCircles = 40;
        beatmap.CountSliders = 50;
        beatmap.CountSpinners = 10;
        beatmap.MaxCombo = 0;
        score.MaxCombo = 100;

        Assert.True(ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap).IsSuccess);
    }

    [Fact]
    public void TestAssertScoreStateRejectsUnusedNativeStandardJudgments()
    {
        var (_, score, beatmap, _, _) = CreateValidQueueEntry();
        beatmap.Convert = false;
        beatmap.ModeInt = (int)Sunrise.Shared.Enums.Beatmaps.GameMode.Standard;
        beatmap.CountCircles = 40;
        beatmap.CountSliders = 50;
        beatmap.CountSpinners = 10;
        score.CountGeki = 1;

        var result = ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap);

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
    }

    [Theory]
    [InlineData(Sunrise.Shared.Enums.Beatmaps.GameMode.Standard, true, 0)]
    [InlineData(Sunrise.Shared.Enums.Beatmaps.GameMode.Taiko, false, 1)]
    [InlineData(Sunrise.Shared.Enums.Beatmaps.GameMode.CatchTheBeat, false, 2)]
    [InlineData(Sunrise.Shared.Enums.Beatmaps.GameMode.Mania, false, 3)]
    public void TestAssertScoreStateSkipsNativeStandardMapChecksForConversionsAndOtherModes(
        Sunrise.Shared.Enums.Beatmaps.GameMode scoreMode, bool convert, int beatmapMode)
    {
        var (_, score, beatmap, _, _) = CreateValidQueueEntry();
        score.GameMode = scoreMode;
        score.MaxCombo = 500;
        beatmap.Convert = convert;
        beatmap.ModeInt = beatmapMode;
        beatmap.CountCircles = 1;
        beatmap.CountSliders = 1;
        beatmap.CountSpinners = 1;
        beatmap.MaxCombo = 10;

        Assert.True(ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap).IsSuccess);
    }

    [Fact]
    public void TestAssertScoreStateRejectsPerfectScoreWithMissInAnyMode()
    {
        var (_, score, beatmap, _, _) = CreateValidQueueEntry();
        score.GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode.Mania;
        score.CountMiss = 1;
        score.Perfect = true;
        beatmap.Convert = true;

        var result = ScoreCandidateBuilderUtil.AssertScoreState(score, beatmap);

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidScoreState, result.Error.Code);
    }

    [Fact]
    public void TestValidateBuiltScoreWithPassedScoreWithoutReplayReturnsReplayMissingError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(replayFileId: null);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(ScoreProcessingErrorCode.ReplayMissing, result.Error.Code);
    }

    [Fact]
    public void TestValidateBuiltScoreWithFailedScoreWithoutReplayReturnsSuccess()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(Mods.None, false, null);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TestValidateBuiltScoreWithInvalidModsReturnsInvalidModsError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(Mods.Target);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(ScoreProcessingErrorCode.InvalidMods, result.Error.Code);
    }

    [Fact]
    public void TestValidateBuiltScoreWithMultipleNonStandardModsReturnsInvalidModsError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(Mods.ScoreV2 | Mods.Relax);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(ScoreProcessingErrorCode.InvalidMods, result.Error.Code);
    }

    [Fact]
    public void TestValidateBuiltScoreWithMismatchedUserHashReturnsInvalidChecksumsError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        queueEntry.UserHash = "other-user-hash";

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);
        Assert.Contains("index: 0", result.Error.Message);
    }

    [Fact]
    public void TestValidateBuiltScoreWithMismatchedScoreHashReturnsInvalidChecksumsError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        buildResult.Value.score.ScoreHash = "different-score-hash";

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);
        Assert.Contains("index: 1", result.Error.Message);
    }

    [Fact]
    public void TestValidateBuiltScoreWithMismatchedBeatmapHashReturnsInvalidChecksumsError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        beatmap.Checksum = "different-beatmap-hash";
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);
        Assert.Contains("index: 2", result.Error.Message);
    }

    private (ScoreSubmissionRequest QueueEntry, Score Score, Beatmap Beatmap, string Username, string ClientHash) CreateValidQueueEntry(
        Mods mods = Mods.None,
        bool isPassed = true,
        int? replayFileId = 1,
        string? storyboardHash = null)
    {
        var user = _mocker.User.GetRandomUser();
        var beatmap = _mocker.Beatmap.GetRandomBeatmap();
        var score = _mocker.Score.GetBestScoreableRandomScore();

        score.EnrichWithUserData(user);
        score.PrepareForSubmission(beatmap);
        score.GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode.Standard;
        beatmap.Convert = true;
        beatmap.MaxCombo = null;
        score.Count300 = 100;
        score.Count100 = score.Count50 = score.CountMiss = 0;
        score.CountGeki = score.CountKatu = 0;
        score.MaxCombo = 100;
        score.Perfect = true;
        score.Grade = isPassed ? ScoreGrade.X : ScoreGrade.F;
        score.OsuVersion = "20260412";
        score.IsScoreable = true;
        score.IsPassed = isPassed;
        score.Mods = mods;
        score.GameMode = score.GameMode.EnrichWithMods(score.Mods);
        score.LocalProperties = score.LocalProperties.FromScore(score);

        var clientHash = "client-hash";
        score.ScoreHash = score.ComputeOnlineHash(user.Username, clientHash, storyboardHash);

        var queueEntry = new ScoreSubmissionRequest
        {
            UserId = user.Id,
            ScoreHash = score.ScoreHash,
            ScoreSerialized = score.ToScoreString(user.Username),
            BeatmapHash = beatmap.Checksum!,
            TimeElapsed = 123,
            OsuVersion = score.OsuVersion,
            ClientHash = clientHash,
            ReplayFileId = replayFileId,
            StoryboardHash = storyboardHash,
            UserHash = clientHash,
            WhenPlayed = score.WhenPlayed
        };

        return (queueEntry, score, beatmap, user.Username, clientHash);
    }
}
