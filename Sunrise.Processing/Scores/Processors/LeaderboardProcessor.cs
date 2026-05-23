using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Utils;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Processors;

[TraceExecution]
public class LeaderboardProcessor(DatabaseService database) : ScoreEntityProcessorBase
{
    public override int Priority => 100;

    protected override Task OnNewSubmissionInternal(ScoreCommitContext ctx)
    {
        ReconcileSubmissionStatus(ctx);
        return Task.CompletedTask;
    }

    protected override Task OnRecalculationInternal(ScoreCommitContext ctx)
    {
        ReconcileSubmissionStatus(ctx);
        return Task.CompletedTask;
    }

    protected override Task OnDeletionInternal(ScoreCommitContext ctx)
    {
        ctx.Score.SubmissionStatus = SubmissionStatus.Deleted;

        ReconcileSubmissionStatus(ctx);
        return Task.CompletedTask;
    }

    protected override Task OnRestorationInternal(ScoreCommitContext ctx)
    {
        var score = ctx.Score;

        score.SubmissionStatus = score.IsPassed
            ? SubmissionStatus.Submitted
            : SubmissionStatus.Failed;

        ReconcileSubmissionStatus(ctx);
        return Task.CompletedTask;
    }

    protected override async Task AfterExecution(ScoreCommitContext ctx)
    {
        database.DbContext.UpdateEntity(ctx.Score);

        if (ctx.UserPersonalBestScores?.SameModsPeer?.BestScoreBasedByTotalScore != null)
            database.DbContext.UpdateEntity(ctx.UserPersonalBestScores.SameModsPeer.BestScoreBasedByTotalScore);

        await database.DbContext.SaveChangesAsync();
    }

    private void ReconcileSubmissionStatus(ScoreCommitContext ctx)
    {
        var score = ctx.Score;

        var sameModsPeer = ctx.UserPersonalBestScores?.SameModsPeer?.BestScoreBasedByTotalScore;

        if (score.SubmissionStatus != SubmissionStatus.Deleted)
            score.UpdateSubmissionStatus(sameModsPeer);

        if (score.SubmissionStatus == SubmissionStatus.Best && sameModsPeer != null)
        {
            sameModsPeer.SubmissionStatus = sameModsPeer.IsPassed
                ? SubmissionStatus.Submitted
                : SubmissionStatus.Failed;

            return;
        }

        var vacatedBest = ctx.OriginalState.SubmissionStatus == SubmissionStatus.Best
                          && score.SubmissionStatus != SubmissionStatus.Best;

        if (vacatedBest && sameModsPeer != null && sameModsPeer.SubmissionStatus != SubmissionStatus.Best)
        {
            sameModsPeer.SubmissionStatus = SubmissionStatus.Best;
        }
    }
}