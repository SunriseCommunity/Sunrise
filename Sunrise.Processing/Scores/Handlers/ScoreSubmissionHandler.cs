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

        var beatmapRatelimitSession = BaseSession.GenerateServerSession();

        var prepareInlineSubmissionCtxAsync = await PrepareInlineSubmissionAsync(beatmapRatelimitSession, payload, ct);
        if (prepareInlineSubmissionCtxAsync.IsFailure)
            return prepareInlineSubmissionCtxAsync.Error;

        return prepareInlineSubmissionCtxAsync;
    }

    internal async Task<Result<ScoreCommitContext, ScoreProcessingError>> PrepareInlineSubmissionAsync(
        BaseSession beatmapRatelimitSession,
        ScoreProcessingQueue queueEntry, CancellationToken ct)
    {
        var loadBeatmapResult = await ResolveBeatmap(beatmapService, beatmapRatelimitSession, queueEntry.BeatmapHash, ct);
        if (loadBeatmapResult.IsFailure)
            return loadBeatmapResult.Error;

        var (beatmapSet, beatmap) = loadBeatmapResult.Value;

        var buildScoreCandidateResult = ScoreCandidateBuilderUtil.Build(queueEntry, beatmap);
        if (buildScoreCandidateResult.IsFailure)
            return buildScoreCandidateResult.Error.ToResult<ScoreCommitContext>();

        var (submittedScore, score) = buildScoreCandidateResult.Value;

        if (Configuration.EnforceLatestClientVersion)
            await CheckScoreClientVersion(score.OsuVersion, queueEntry.OsuVersion, ct);

        var validateBuiltScoreResult = ScoreCandidateBuilderUtil.ValidateBuiltScore(queueEntry, score, submittedScore, beatmap.Checksum ?? string.Empty);

        if (validateBuiltScoreResult.IsFailure)
        {
            await RestrictUserIfErrorCodeIsBannable(score.UserId, validateBuiltScoreResult.Error.Code);
            return validateBuiltScoreResult.Error.ToResult<ScoreCommitContext>();
        }

        var scorePerformanceResult = await calculatorService.CalculateScorePerformance(beatmapRatelimitSession, score, ct: ct);
        if (scorePerformanceResult.IsFailure)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.PpCalculationFailed,
                    "PP calculation failed: " + scorePerformanceResult.Error.Message,
                    ScoreProcessingDisposition.Retryable)
                .ToResult<ScoreCommitContext>();

        if (scorePerformanceResult.Value == null)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.PpCalculationFailed,
                    "Score performance calculation returned null",
                    ScoreProcessingDisposition.Retryable)
                .ToResult<ScoreCommitContext>();

        score.PerformancePoints = scorePerformanceResult.Value.PerformancePoints;

        var validateScorePerformanceResult = ValidateScorePerformance(score, ct);

        if (validateScorePerformanceResult.IsFailure)
        {
            await RestrictUserIfErrorCodeIsBannable(score.UserId, validateScorePerformanceResult.Error.Code);
            return validateScorePerformanceResult.Error.ToResult<ScoreCommitContext>();
        }

        var loadUserStateResult = await LoadUserState(score, ct);
        if (loadUserStateResult.IsFailure)
            return loadUserStateResult.Error.ToResult<ScoreCommitContext>();

        var (user, userStats, userGrades) = loadUserStateResult.Value;

        _prevUserStatsSnapshot = userStats.Clone();

        var ctx = new ScoreCommitContext(ScoreTaskType.Submission, score, user, userStats, userGrades, beatmap, beatmapSet);
        return ctx;
    }

    public async Task<Result<string?, ScoreProcessingError>> ExecuteInlineSubmission(
        BaseSession beatmapRatelimitSession,
        ScoreProcessingQueue queueEntry,
        CancellationToken ct,
        ScoreTaskQueue? task = null)
    {
        var prepareResult = await PrepareInlineSubmissionAsync(beatmapRatelimitSession, queueEntry, ct);
        if (prepareResult.IsFailure)
            return prepareResult.Error;

        var ctx = prepareResult.Value;

        var commitResult = await CommitAsync(prepareResult.Value, task, ct);
        if (commitResult.IsFailure)
            return commitResult.Error;

        var newAchievements = await scoreSideEffectsPublisherService.PublishScoreSideEffectsAndReturnNewAchievements(BaseSession.GenerateServerSession(), ctx, ct);

        var shouldReturnScoreResponseString = ctx.Beatmap?.IsScoreable ?? false;

        if (!shouldReturnScoreResponseString)
            return null;

        var responseString = await scoreSideEffectsPublisherService.BuildScoreSubmitResponse(ctx, newAchievements, _prevUserStatsSnapshot!, ct);

        return responseString;
    }

    internal override async Task OnCommitted(ScoreCommitContext ctx, CancellationToken ct)
    {
        await scoreSideEffectsPublisherService.PublishScoreSideEffectsAndReturnNewAchievements(BaseSession.GenerateServerSession(), ctx, ct);
    }

    private UnitResult<ScoreProcessingError> ValidateScorePerformance(Score score, CancellationToken ct)
    {
        var hasNonStandardModsForBanCheck = score.Mods.TryGetSelectedNotStandardMods() is not Mods.None;
        var isScoreBannable = score.PerformancePoints >= Configuration.BannablePpThreshold
                              && !hasNonStandardModsForBanCheck
                              && score.LocalProperties.IsRanked;

        if (isScoreBannable)
            return new ScoreProcessingError(ScoreProcessingErrorCode.BannablePpThreshold, "Too many PP - auto-restricted").ToUnit();

        return UnitResult.Success<ScoreProcessingError>();
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

    private async Task RestrictUserIfErrorCodeIsBannable(int userId, ScoreProcessingErrorCode errorCode)
    {
        var reason = errorCode switch
        {
            ScoreProcessingErrorCode.BannablePpThreshold => "Auto-restricted for submitting impossible score",
            ScoreProcessingErrorCode.InvalidChecksums => "Invalid checksums on score submission",
            _ => null
        };

        if (reason != null)
        {
            Log.Error("Score submission failed with error code {ErrorCode} for user {UserId}. Restriction reason: {Reason}",
                errorCode,
                userId,
                reason);

            await Database.Users.Moderation.RestrictPlayer(userId, null, reason);
        }
    }
}