using Sunrise.Processing.Utils;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Xunit;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using Mods = osu.Shared.Mods;

namespace Sunrise.Processing.Tests.Utils;

public class ScoreCandidateBuilderUtilTests : BaseTest
{
    private readonly MockService _mocker = new();

    [Fact]
    public void TestBuildWithValidQueueEntryReturnsScoreAndSubmittedScore()
    {
        // Arrange
        var (queueEntry, originalScore, beatmap, username, _) = CreateValidQueueEntry(replayFileId: 321);

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
        Assert.Equal(321, result.Value.score.ReplayFileId);
    }

    [Fact]
    public void TestBuildWithInvalidScoreStringReturnsParsedScoreInvalidError()
    {
        // Arrange
        var beatmap = CreateBeatmap();
        var queueEntry = new ScoreProcessingQueue
        {
            UserId = 77,
            ScoreHash = "score-hash",
            ScoreSerialized = "invalid",
            BeatmapHash = beatmap.Checksum!,
            TimeElapsed = 123,
            OsuVersion = "b20260101.1",
            ClientHash = "client-hash",
            UserHash = "client-hash",
            WhenPlayed = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        };

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
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(replayFileId: 321);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap.Checksum!);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TestValidateBuiltScoreWithPassedScoreWithoutReplayReturnsReplayMissingError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(replayFileId: null);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap.Checksum!);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.ReplayMissing, result.Error.Code);
    }

    [Fact]
    public void TestValidateBuiltScoreWithFailedScoreWithoutReplayReturnsSuccess()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(mods: Mods.None, isPassed: false, replayFileId: null);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap.Checksum!);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TestValidateBuiltScoreWithInvalidModsReturnsInvalidModsError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(Mods.Target, replayFileId: 321);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap.Checksum!);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidMods, result.Error.Code);
    }

    [Fact]
    public void TestValidateBuiltScoreWithMultipleNonStandardModsReturnsNonStandardModsUnsupportedError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(Mods.ScoreV2 | Mods.Relax, replayFileId: 321);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap.Checksum!);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.NonStandardModsUnsupported, result.Error.Code);
    }

    [Fact]
    public void TestValidateBuiltScoreWithMismatchedUserHashReturnsInvalidChecksumsError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(replayFileId: 321);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);
        queueEntry.UserHash = "other-user-hash";

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap.Checksum!);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);
        Assert.Contains("index: 0", result.Error.Message);
    }

    [Fact]
    public void TestValidateBuiltScoreWithMismatchedScoreHashReturnsInvalidChecksumsError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(replayFileId: 321);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);
        buildResult.Value.score.ScoreHash = "different-score-hash";

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, beatmap.Checksum!);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);
        Assert.Contains("index: 1", result.Error.Message);
    }

    [Fact]
    public void TestValidateBuiltScoreWithMismatchedBeatmapHashReturnsInvalidChecksumsError()
    {
        // Arrange
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(replayFileId: 321);
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, "different-beatmap-hash");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidChecksums, result.Error.Code);
        Assert.Contains("index: 2", result.Error.Message);
    }

    private (ScoreProcessingQueue QueueEntry, Score Score, Beatmap Beatmap, string Username, string ClientHash) CreateValidQueueEntry(
        Mods mods = Mods.None,
        bool isPassed = true,
        int? replayFileId = 321,
        string? storyboardHash = null)
    {
        var beatmap = CreateBeatmap();
        var score = _mocker.Score.GetBestScoreableRandomScore();

        score.UserId = 77;
        score.BeatmapId = beatmap.Id;
        score.BeatmapHash = beatmap.Checksum!;
        score.BeatmapStatus = BeatmapStatus.Ranked;
        score.IsScoreable = true;
        score.IsPassed = isPassed;
        score.GameMode = mods == Mods.Relax ? GameMode.RelaxStandard : GameMode.Standard;
        score.Mods = mods;
        score.OsuVersion = "b20260101.1";
        score.WhenPlayed = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        score.ClientTime = new DateTime(2026, 1, 2, 3, 4, 5);
        score.LocalProperties = score.LocalProperties.FromScore(score);

        var username = "player";
        var clientHash = "client-hash";
        score.ScoreHash = score.ComputeOnlineHash(username, clientHash, storyboardHash);

        var queueEntry = new ScoreProcessingQueue
        {
            UserId = 77,
            ScoreHash = score.ScoreHash,
            ScoreSerialized = score.ToScoreString(username),
            BeatmapHash = beatmap.Checksum!,
            TimeElapsed = 123,
            OsuVersion = score.OsuVersion,
            ClientHash = clientHash,
            ReplayFileId = replayFileId,
            StoryboardHash = storyboardHash,
            UserHash = clientHash,
            WhenPlayed = score.WhenPlayed
        };

        return (queueEntry, score, beatmap, username, clientHash);
    }

    private static Beatmap CreateBeatmap()
    {
        return new Beatmap
        {
            Id = 11,
            BeatmapsetId = 22,
            DifficultyRating = 5,
            Mode = "osu",
            StatusString = "ranked",
            TotalLength = 120,
            UserId = 99,
            Version = "Insane",
            BPM = 180,
            HitLength = 100,
            LastUpdated = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            ModeInt = (int)GameMode.Standard.ToVanillaGameMode(),
            Passcount = 44,
            Playcount = 33,
            Ranked = (int)BeatmapStatus.Ranked,
            Url = "https://example/map",
            Checksum = "beatmap-hash"
        };
    }
}