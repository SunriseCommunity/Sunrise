using CSharpFunctionalExtensions;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Handlers;

public class ScoreRestorationHandler(
    DatabaseService database,
    ScoreCommitPipeline pipeline)
    : ScoreHandlerBase(database, pipeline)
{

    internal override async Task<Result<ScoreCommitContext, ScoreProcessingError>> PrepareAsync(
        ScoreTaskQueue task, CancellationToken ct)
    {
        var score = await Database.Scores.GetScore(task.ScoreId!.Value, filterValidScores: false, ct: ct);
        if (score == null)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.Unexpected,
                    $"Score {task.ScoreId} not found")
                .ToResult<ScoreCommitContext>();

        if (score.SubmissionStatus != SubmissionStatus.Deleted)
            return new ScoreProcessingError(
                    ScoreProcessingErrorCode.InvalidScoreState,
                    $"Score {task.ScoreId} is not deleted")
                .ToResult<ScoreCommitContext>();

        var loadUserStateResult = await LoadUserState(score, ct);
        if (loadUserStateResult.IsFailure)
            return loadUserStateResult.Error.ToResult<ScoreCommitContext>();

        var (user, userStats, userGrades) = loadUserStateResult.Value;
        var ctx = new ScoreCommitContext(ScoreTaskType.Restore, score, user, userStats, userGrades);
        return ctx;
    }
}