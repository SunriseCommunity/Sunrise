using CSharpFunctionalExtensions;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Handlers;

public class ScoreDeletionHandler(
    DatabaseService database,
    ScoreCommitPipeline pipeline)
    : ScoreHandlerBase(database, pipeline)
{
    internal override async Task<Result<ScorePrepareContext, ScoreProcessingError>> PrepareAsync(ScoreProcessingTask task, CancellationToken ct)
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
                $"Score {task.ScoreId} is already deleted"
            ).ToResult<ScorePrepareContext>();

        return new ScorePrepareContext(ScoreTaskType.Delete, score);
    }
}