using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Processors;

[TraceExecution]
public class UserGradesScoreProcessor(DatabaseService database) : ScoreEntityProcessorBase
{
    public override int Priority => 200;

    protected override Task OnNewSubmissionInternal(ScoreCommitContext ctx)
    {
        IncrementWithScore(ctx);
        return Task.CompletedTask;
    }

    protected override Task OnRecalculationInternal(ScoreCommitContext ctx)
    {
        return Task.CompletedTask;
    }

    protected override Task OnBeatmapStatusChangeInternal(ScoreCommitContext ctx)
    {
        var previousStatus = ctx.Score.BeatmapHashStatus?.PreviousStatus;

        if (previousStatus != null && previousStatus.Value.IsScoreable() != ctx.BeatmapStatus.IsScoreable())
            ctx.UserGrades.UpdateGradeCount(ctx.Score.Grade, ctx.BeatmapStatus.IsScoreable() ? 1 : -1);

        return Task.CompletedTask;
    }

    protected override Task OnDeletionInternal(ScoreCommitContext ctx)
    {
        DecrementWithScore(ctx);
        return Task.CompletedTask;
    }

    protected override Task OnRestorationInternal(ScoreCommitContext ctx)
    {
        IncrementWithScore(ctx);
        return Task.CompletedTask;
    }

    protected override async Task AfterExecution(ScoreCommitContext ctx)
    {
        // NOTE: Ideally we should have atomic update here, but we have an assumption that pp calculation and beatmap retrieval would
        // be the heaviest operations. Thus, just relying on lock FOR UPDATES is enough in this context.
        var updateUserGradesResult = await database.Users.Grades.UpdateUserGrades(ctx.UserGrades);
        if (updateUserGradesResult.IsFailure)
            throw new ApplicationException("Failed to persist user grades: " + updateUserGradesResult.Error);
    }

    private static void IncrementWithScore(ScoreCommitContext ctx)
    {
        var score = ctx.Score;
        var userGrades = ctx.UserGrades;
        var previousOverallBest = ctx.UserPersonalBestScores?.OverallPeer?.BestScoreByScoreValue;

        var isFailed = !score.IsPassed;
        if (isFailed || !ctx.BeatmapStatus.IsScoreable() || score.SubmissionStatus != SubmissionStatus.Best)
            return;

        if (!IsOverallBestScore(score, previousOverallBest))
            return;

        // If the grade hasn't changed, we can return early and skip any DB calls.
        if (previousOverallBest?.Grade == score.Grade)
            return;

        if (previousOverallBest != null)
            userGrades.UpdateGradeCount(previousOverallBest.Grade, -1);

        userGrades.UpdateGradeCount(score.Grade, 1);
    }

    private static void DecrementWithScore(ScoreCommitContext ctx)
    {
        var score = ctx.Score;
        var userGrades = ctx.UserGrades;
        var original = ctx.OriginalState;
        var promotedOverallBest = ctx.UserPersonalBestScores?.OverallPeer?.BestScoreByScoreValue;

        var isFailed = !original.IsPassed;
        if (isFailed || !original.IsScoreable || original.SubmissionStatus != SubmissionStatus.Best)
            return;

        if (!IsOverallBestScore(score, promotedOverallBest))
            return;

        // If the grade hasn't changed, we can return early and skip any DB calls.
        if (promotedOverallBest?.Grade == score.Grade)
            return;

        if (promotedOverallBest != null)
            userGrades.UpdateGradeCount(promotedOverallBest.Grade, 1);

        userGrades.UpdateGradeCount(score.Grade, -1);
    }

    private static bool IsOverallBestScore(Score score, Score? peer)
    {
        if (peer == null)
            return true;

        return new List<Score>
            {
                score,
                peer
            }
            .SortScoresByTheirScoreValue()
            .First() == score;
    }
}