using CSharpFunctionalExtensions;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Objects;
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
    internal override async Task<Result<ScorePrepareContext, ScoreProcessingError>> PrepareAsync(
        ScoreProcessingTask task, CancellationToken ct)
    {
        var score = await Database.Scores.GetScore(task.ScoreId!.Value, new QueryOptions(true), filterValidScores: false, ct: ct);
        if (score == null)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.Unexpected,
                    $"Score {task.ScoreId} not found")
                .ToResult<ScorePrepareContext>();

        if (score.SubmissionStatus == SubmissionStatus.Deleted)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.InvalidScoreState,
                    $"Score {task.ScoreId} is deleted; use RestoreScore to bring it back")
                .ToResult<ScorePrepareContext>();

        var beatmapRatelimitSession = BaseSession.GenerateServerSession();

        var loadBeatmapResult = await ResolveBeatmap(beatmapService, beatmapRatelimitSession, score.BeatmapHash, ct);
        if (loadBeatmapResult.IsFailure)
            return loadBeatmapResult.Error.ToResult<ScorePrepareContext>();

        var (_, beatmap) = loadBeatmapResult.Value;

        var scorePerformanceResult = await calculatorService.CalculateScorePerformance(beatmapRatelimitSession, score, ct: ct);
        if (scorePerformanceResult.IsFailure)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.PpCalculationFailed,
                    "PP calculation failed: " + scorePerformanceResult.Error.Message,
                    ScoreProcessingDisposition.Retryable)
                .ToResult<ScorePrepareContext>();

        if (scorePerformanceResult.Value == null)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.PpCalculationFailed,
                    "Score performance calculation returned null",
                    ScoreProcessingDisposition.Retryable)
                .ToResult<ScorePrepareContext>();

        score.PerformancePoints = scorePerformanceResult.Value.PerformancePoints;
        return new ScorePrepareContext(
            ScoreTaskType.Recalculation,
            score,
            scorePerformanceResult.Value.PerformancePoints,
            beatmap);
    }
}