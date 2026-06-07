using CSharpFunctionalExtensions;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Handlers;

public class ScoreRecalculationHandler(
    DatabaseService database,
    ScoreCommitPipeline pipeline,
    BeatmapService beatmapService,
    CalculatorService calculatorService)
    : ScoreHandlerBase(database, pipeline)
{
    internal override async Task<Result<ScoreCommitContext, ScoreProcessingError>> PrepareAsync(
        ScoreProcessingTask task, CancellationToken ct)
    {
        var score = await Database.Scores.GetScore(task.ScoreId!.Value, filterValidScores: false, ct: ct);
        if (score == null)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.Unexpected,
                    $"Score {task.ScoreId} not found")
                .ToResult<ScoreCommitContext>();

        if (score.SubmissionStatus == SubmissionStatus.Deleted)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.InvalidScoreState,
                    $"Score {task.ScoreId} is deleted; use RestoreScore to bring it back")
                .ToResult<ScoreCommitContext>();

        var beatmapRatelimitSession = BaseSession.GenerateServerSession();

        var loadBeatmapResult = await ResolveBeatmap(beatmapService, beatmapRatelimitSession, score.BeatmapHash, ct);
        if (loadBeatmapResult.IsFailure)
            return loadBeatmapResult.Error.ToResult<ScoreCommitContext>();

        var (_, beatmap) = loadBeatmapResult.Value;

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

        var loadUserStateResult = await LoadUserState(score, ct);
        if (loadUserStateResult.IsFailure)
            return loadUserStateResult.Error.ToResult<ScoreCommitContext>();

        var (user, userStats, userGrades) = loadUserStateResult.Value;
        var ctx = new ScoreCommitContext(ScoreTaskType.Recalculation, score, user, userStats, userGrades, beatmap);
        return ctx;
    }
}