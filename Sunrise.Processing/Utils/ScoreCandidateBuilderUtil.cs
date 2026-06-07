using CSharpFunctionalExtensions;
using Serilog;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Utils;

namespace Sunrise.Processing.Utils;

public static class ScoreCandidateBuilderUtil
{
    public static Result<(SubmittedScore submittedScore, Score score), ScoreProcessingError> Build(ScoreSubmissionRequest queueEntry, Beatmap beatmap)
    {
        var parsedScoreResult = queueEntry.ScoreSerialized.TryParseBaseScore(queueEntry.WhenPlayed);

        if (parsedScoreResult.IsFailure)
        {
            return new ScoreProcessingError(ScoreProcessingErrorCode.ParsedScoreInvalid, parsedScoreResult.Error)
                .ToResult<(SubmittedScore submittedScore, Score score)>();
        }

        var submittedScore = parsedScoreResult.Value;
        var score = submittedScore.ToScore(queueEntry.UserId, beatmap, queueEntry.TimeElapsed);

        if (queueEntry.ReplayFileId.HasValue)
            score.ReplayFileId = queueEntry.ReplayFileId.Value;

        return (submittedScore, score);
    }

    public static UnitResult<ScoreProcessingError> ValidateBuiltScore(ScoreSubmissionRequest queueEntry, Score score, SubmittedScore submittedScore, string onlineBeatmapChecksum)
    {
        var failureValidators = new[]
        {
            () => AssertPassedScoreHasReplay(score, queueEntry.ScoreSerialized),
            () => AssertScoreMods(score, queueEntry.ScoreSerialized),
            () => AssertScoreHashes(
                queueEntry.UserHash,
                score,
                queueEntry.ClientHash,
                queueEntry.BeatmapHash,
                onlineBeatmapChecksum,
                queueEntry.StoryboardHash,
                submittedScore.PlayerUsername)
        };

        foreach (var validate in failureValidators)
        {
            var result = validate();
            if (result.IsFailure)
                return result;
        }

        return UnitResult.Success<ScoreProcessingError>();
    }

    private static UnitResult<ScoreProcessingError> AssertScoreHashes(string userHash, Score score, string clientHash,
        string beatmapHash, string onlineBeatmapHash, string? storyboardHash, string sessionUsername)
    {
        var computedOnlineHash = score.ComputeOnlineHash(sessionUsername.Trim(), clientHash, storyboardHash);
        var checks = new[]
        {
            string.Equals(clientHash, userHash, StringComparison.Ordinal),
            string.Equals(score.ScoreHash, computedOnlineHash, StringComparison.Ordinal),
            string.Equals(beatmapHash, onlineBeatmapHash, StringComparison.Ordinal)
        };

        foreach (var (isHashCorrect, i) in checks.Select((value, index) => (value, index)))
        {
            if (isHashCorrect)
                continue;

            Log.Warning(
                "Score submission rejected for user {UserId}. ClientHash: {ClientHash}, UserHash: {UserHash}, ScoreHash: {ScoreHash}, ComputedOnlineHash: {ComputedOnlineHash}, BeatmapHash: {BeatmapHash}, OnlineBeatmapHash: {OnlineBeatmapHash}, StoryboardHash: {StoryboardHash} (Invalid checksums on score submission)",
                score.UserId,
                clientHash,
                userHash,
                score.ScoreHash,
                computedOnlineHash,
                beatmapHash,
                onlineBeatmapHash,
                storyboardHash);

            return new ScoreProcessingError(ScoreProcessingErrorCode.InvalidChecksums, $"Invalid checksums for entry with index: {i}").ToUnit();
        }

        return UnitResult.Success<ScoreProcessingError>();
    }

    private static UnitResult<ScoreProcessingError> AssertPassedScoreHasReplay(Score score, string scoreSerialized)
    {
        var isCurrentScoreFailed = ScoreSubmissionUtil.IsScoreFailed(score);

        if (isCurrentScoreFailed || score.ReplayFileId != null)
            return UnitResult.Success<ScoreProcessingError>();

        Log.Error("Replay file not found for passed score {score}", scoreSerialized);
        return new ScoreProcessingError(ScoreProcessingErrorCode.ReplayMissing, "Replay file not found for passed score").ToUnit();
    }

    private static UnitResult<ScoreProcessingError> AssertScoreMods(Score score, string scoreSerialized)
    {
        var validateScoreModsResult = ModsValidationUtil.ValidateMods(score.Mods, score.GameMode.ToVanillaGameMode());

        if (validateScoreModsResult.IsFailure)
        {
            Log.Warning("Invalid mods found on score {score}, {errorMsg}", scoreSerialized, validateScoreModsResult.Error);
            return new ScoreProcessingError(ScoreProcessingErrorCode.InvalidMods, validateScoreModsResult.Error).ToUnit();
        }

        return UnitResult.Success<ScoreProcessingError>();
    }
}