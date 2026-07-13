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
            return new ScoreProcessingError(ScoreProcessingErrorCode.ParsedScoreInvalid, parsedScoreResult.Error)
                .ToResult<(SubmittedScore submittedScore, Score score)>();

        var submittedScore = parsedScoreResult.Value;
        var score = submittedScore.ToScore(queueEntry.UserId, beatmap, queueEntry.TimeElapsed);

        if (queueEntry.ReplayFileId.HasValue)
            score.ReplayFileId = queueEntry.ReplayFileId.Value;

        return (submittedScore, score);
    }

    public static UnitResult<ScoreProcessingError> ValidateBuiltScore(ScoreSubmissionRequest queueEntry, Score score, SubmittedScore submittedScore, Beatmap beatmap)
    {
        var failureValidators = new[]
        {
            () => AssertPassedScoreHasReplay(score, queueEntry.ScoreSerialized),
            () => AssertScoreMods(score, queueEntry.ScoreSerialized),
            () => AssertScoreState(score, submittedScore, beatmap),
            () => AssertGrade(score, submittedScore),
            () => AssertClientVersions(score.OsuVersion, queueEntry.OsuVersion),
            () => AssertScoreHashes(
                queueEntry.UserHash,
                score,
                queueEntry.ClientHash,
                queueEntry.BeatmapHash,
                beatmap.Checksum ?? string.Empty,
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

    private static UnitResult<ScoreProcessingError> AssertClientVersions(string scoreVersion, string formVersion)
    {
        return OsuVersion.IsValidClientVersion(scoreVersion) && OsuVersion.IsValidClientVersion(formVersion)
            ? UnitResult.Success<ScoreProcessingError>()
            : new ScoreProcessingError(ScoreProcessingErrorCode.InvalidClientVersion, "Invalid osu! client version").ToUnit();
    }

    private static UnitResult<ScoreProcessingError> AssertGrade(Score score, SubmittedScore submittedScore)
    {
        var expected = ScoreGradeUtil.Calculate(submittedScore).ToString();
        if (string.Equals(score.Grade, expected, StringComparison.Ordinal))
            return UnitResult.Success<ScoreProcessingError>();

        Log.Warning("Invalid grade {Grade}; expected {ExpectedGrade} for submitted score by user {UserId}", score.Grade, expected, score.UserId);
        return new ScoreProcessingError(ScoreProcessingErrorCode.InvalidGrade, $"Invalid grade; expected {expected}").ToUnit();
    }

    private static UnitResult<ScoreProcessingError> AssertScoreState(Score score, SubmittedScore submittedScore, Beatmap beatmap)
    {
        var mode = score.GameMode.ToVanillaGameMode();
        var primaryHits = mode switch
        {
            osu.Shared.GameMode.Standard => score.Count300 + score.Count100 + score.Count50 + score.CountMiss,
            osu.Shared.GameMode.Taiko => score.Count300 + score.Count100 + score.CountMiss,
            osu.Shared.GameMode.CatchTheBeat => score.Count300 + score.Count100 + score.Count50 + score.CountKatu + score.CountMiss,
            osu.Shared.GameMode.Mania => score.Count300 + score.Count100 + score.Count50 + score.CountGeki + score.CountKatu + score.CountMiss,
            _ => -1
        };

        string? error = null;
        if (primaryHits <= 0)
            error = "Score has no judgments";
        else if (beatmap.MaxCombo is > 0 && score.MaxCombo > beatmap.MaxCombo)
            error = "Maximum combo exceeds beatmap maximum combo";
        else if (score.MaxCombo > primaryHits && mode is not osu.Shared.GameMode.CatchTheBeat)
            error = "Maximum combo exceeds submitted judgments";
        else if (score.Perfect && (score.CountMiss != 0 || beatmap.MaxCombo is > 0 && score.MaxCombo != beatmap.MaxCombo))
            error = "Perfect flag is inconsistent with misses or beatmap maximum combo";
        else if (mode == osu.Shared.GameMode.Taiko && (score.Count50 != 0 || score.CountGeki != 0 || score.CountKatu != 0))
            error = "Taiko score contains unused judgments";
        else if (mode == osu.Shared.GameMode.CatchTheBeat && score.CountGeki != 0)
            error = "Catch score contains unused judgments";
        else if (mode == osu.Shared.GameMode.Standard && !beatmap.Convert)
        {
            var objectCount = beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners;
            if (primaryHits > objectCount || score.IsPassed && primaryHits != objectCount)
                error = "Standard judgment count does not match beatmap object count";
        }
        else if (mode == osu.Shared.GameMode.Taiko && !beatmap.Convert && beatmap.CountCircles > 0 &&
                 (primaryHits > beatmap.CountCircles || score.IsPassed && primaryHits != beatmap.CountCircles))
            error = "Taiko judgment count does not match beatmap note count";

        if (error == null) return UnitResult.Success<ScoreProcessingError>();

        Log.Warning("Invalid score state for user {UserId}: {Error}; score={Score}", score.UserId, error, submittedScore);
        return new ScoreProcessingError(ScoreProcessingErrorCode.InvalidScoreState, error).ToUnit();
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

        if (!validateScoreModsResult.IsFailure) return UnitResult.Success<ScoreProcessingError>();

        Log.Warning("Invalid mods found on score {score}, {errorMsg}", scoreSerialized, validateScoreModsResult.Error);
        return new ScoreProcessingError(ScoreProcessingErrorCode.InvalidMods, validateScoreModsResult.Error).ToUnit();
    }
}
