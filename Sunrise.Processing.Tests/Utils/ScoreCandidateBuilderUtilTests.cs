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
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(Mods.None, false, null);
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
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(Mods.Target);
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
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry(Mods.ScoreV2 | Mods.Relax);
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
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
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
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
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
        var (queueEntry, _, beatmap, _, _) = CreateValidQueueEntry();
        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);

        // Act
        var result = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, buildResult.Value.score, buildResult.Value.submittedScore, "different-beatmap-hash");

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
        score.EnrichWithBeatmapData(beatmap);
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