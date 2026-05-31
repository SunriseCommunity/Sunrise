using CSharpFunctionalExtensions;
using osu.Shared;
using Serilog;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Services;
using Sunrise.Processing.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Processing.Scores.Handlers;

public class ScoreSubmissionHandler(
    DatabaseService database,
    ScoreCommitPipeline pipeline,
    BeatmapService beatmapService,
    CalculatorService calculatorService,
    OsuVersionService osuVersionService,
    ScoreSideEffectsPublisherService scoreSideEffectsPublisherService)
    : ScoreHandlerBase(database, pipeline)
{
    private UserStats? _prevUserStatsSnapshot;

    internal override async Task<Result<ScoreCommitContext, ScoreProcessingError>> PrepareAsync(
        ScoreTaskQueue task, CancellationToken ct)
    {
        if (!task.ScoreProcessingQueueId.HasValue)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.Unexpected,
                    $"Submission task {task.Id} is missing its payload reference")
                .ToResult<ScoreCommitContext>();

        var payload = await Database.ScoreProcessingQueue.GetById(task.ScoreProcessingQueueId.Value, ct);
        if (payload == null)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.Unexpected,
                    $"Submission payload {task.ScoreProcessingQueueId.Value} was not found for task {task.Id}")
                .ToResult<ScoreCommitContext>();

        return await PrepareFromPayload(BaseSession.GenerateServerSession(), payload, ct);
    }

    internal override async Task OnCommitted(ScoreCommitContext ctx, CancellationToken ct)
    {
        if (!IsScoreScoreable(ctx.Score) || ctx.BeatmapSet == null || ctx.Beatmap == null)
            return;

        await scoreSideEffectsPublisherService.PublishScoreSideEffectsAndBuildSubmissionResponse(
            BaseSession.GenerateServerSession(),
            ctx,
            _prevUserStatsSnapshot!,
            ct);
    }

    public async Task<Result<string?, ScoreProcessingError>> ProcessInlineSubmission(
        BaseSession beatmapRatelimitSession,
        ScoreProcessingQueue queueEntry,
        CancellationToken ct,
        ScoreTaskQueue? task = null)
    {
        var prepareResult = await PrepareFromPayload(beatmapRatelimitSession, queueEntry, ct);
        if (prepareResult.IsFailure)
            return prepareResult.Error;

        var ctx = prepareResult.Value;

        var commitResult = await pipeline.Commit(ctx, task, ct);

        if (commitResult.IsFailure)
        {
            var translated = TryTranslateTransactionFailure(commitResult.Error);
            if (translated.IsFailure)
                return translated.Error;

            Log.Warning("Failed to commit score state mutation, reason: {Reason}, ScoreId: {ScoreId}",
                commitResult.Error,
                ctx.Score.Id);

            return new ScoreProcessingError(
                ScoreProcessingErrorCode.TransactionFailed,
                $"Failed to apply score state changes: {commitResult.Error}",
                ScoreProcessingDisposition.Retryable);
        }

        if (!IsScoreScoreable(ctx.Score) || ctx.BeatmapSet == null || ctx.Beatmap == null)
            return Result.Success<string?, ScoreProcessingError>(null);

        var response = await scoreSideEffectsPublisherService.PublishScoreSideEffectsAndBuildSubmissionResponse(
            beatmapRatelimitSession,
            ctx,
            _prevUserStatsSnapshot!,
            ct);

        return Result.Success<string?, ScoreProcessingError>(response);
    }

    private async Task<Result<ScoreCommitContext, ScoreProcessingError>> PrepareFromPayload(
        BaseSession beatmapRatelimitSession,
        ScoreProcessingQueue queueEntry,
        CancellationToken ct)
    {
        var loadBeatmapResult = await ResolveBeatmap(beatmapService, beatmapRatelimitSession, queueEntry.BeatmapHash, ct);
        if (loadBeatmapResult.IsFailure)
            return loadBeatmapResult.Error.ToResult<ScoreCommitContext>();

        var (beatmapSet, beatmap) = loadBeatmapResult.Value;

        var buildResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);
        if (buildResult.IsFailure)
            return buildResult.Error.ToResult<ScoreCommitContext>();

        var (submittedScore, score) = buildResult.Value;
        score.TimeElapsed = queueEntry.TimeElapsed;

        if (Configuration.EnforceLatestClientVersion)
            await CheckScoreClientVersion(score.OsuVersion, queueEntry.OsuVersion, ct);

        var validationResult = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, score, submittedScore, beatmap.Checksum ?? string.Empty);

        if (validationResult.IsFailure)
        {
            await RestrictUserForInvalidChecksums(score.UserId, validationResult.Error.Code);
            return validationResult.Error.ToResult<ScoreCommitContext>();
        }

        var computeResult = await ComputePerformanceAndValidate(beatmapRatelimitSession, score, ct);
        if (computeResult.IsFailure)
            return computeResult.Error.ToResult<ScoreCommitContext>();

        var loadUserStateResult = await LoadUserState(score, ct);
        if (loadUserStateResult.IsFailure)
            return loadUserStateResult.Error.ToResult<ScoreCommitContext>();

        var (user, userStats, userGrades) = loadUserStateResult.Value;

        _prevUserStatsSnapshot = userStats.Clone();

        var ctx = new ScoreCommitContext(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap, beatmapSet);
        return ctx;
    }

    private async Task<UnitResult<ScoreProcessingError>> ComputePerformanceAndValidate(
        BaseSession session, Score score, CancellationToken ct)
    {
        var scorePerformanceResult = await calculatorService.CalculateScorePerformance(session, score, ct: ct);
        if (scorePerformanceResult.IsFailure)
            return new ScoreProcessingError(ScoreProcessingErrorCode.PpCalculationFailed,
                "PP calculation failed: " + scorePerformanceResult.Error.Message,
                ScoreProcessingDisposition.Retryable).ToUnit();

        if (scorePerformanceResult.Value == null)
            return new ScoreProcessingError(ScoreProcessingErrorCode.PpCalculationFailed,
                "Score performance calculation returned null",
                ScoreProcessingDisposition.Retryable).ToUnit();

        score.PerformancePoints = scorePerformanceResult.Value.PerformancePoints;

        var hasNonStandardModsForBanCheck = score.Mods.TryGetSelectedNotStandardMods() is not Mods.None;
        var isScoreBannable = score.PerformancePoints >= Configuration.BannablePpThreshold
                              && !hasNonStandardModsForBanCheck
                              && score.LocalProperties.IsRanked;

        if (isScoreBannable)
        {
            Log.Error("Too many performance points. Cheating? ScoreId: {scoreId}", score.Id);
            await Database.Users.Moderation.RestrictPlayer(score.UserId, null, "Auto-restricted for submitting impossible score");
            return new ScoreProcessingError(ScoreProcessingErrorCode.BannablePpThreshold, "Too many PP - auto-restricted").ToUnit();
        }

        return UnitResult.Success<ScoreProcessingError>();
    }

    private static bool IsScoreScoreable(Score score)
    {
        var isCurrentScoreFailed = ScoreSubmissionUtil.IsScoreFailed(score);
        return !isCurrentScoreFailed && score.IsScoreable;
    }

    private async Task CheckScoreClientVersion(string scoreOsuVersion, string formOsuVersion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var versionString = !string.IsNullOrWhiteSpace(scoreOsuVersion) ? scoreOsuVersion : formOsuVersion;
        var clientVersion = OsuVersion.TryParse(versionString);
        if (clientVersion == null)
            return;

        var latestVersion = await osuVersionService.GetLatestVersion(clientVersion.Stream);
        if (latestVersion == null)
            return;

        if (clientVersion < latestVersion)
            Log.Warning("Score submitted with outdated osu! client version {ClientVersion} (stream: {Stream}, latest: {LatestVersion})",
                clientVersion,
                clientVersion.Stream,
                latestVersion);
    }

    private async Task RestrictUserForInvalidChecksums(int userId, ScoreProcessingErrorCode errorCode)
    {
        if (errorCode != ScoreProcessingErrorCode.InvalidChecksums)
            return;

        await Database.Users.Moderation.RestrictPlayer(userId, null, "Invalid checksums on score submission");
    }
}