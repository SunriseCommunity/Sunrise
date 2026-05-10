using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Processing.Utils;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Processors;

[TraceExecution]
public class LeaderboardProcessor(DatabaseService database) : IScoreEntityProcessor
{
    public int Priority => 100;

    public async Task OnNewSubmission(ScoreCommitContext ctx)
    {
        await ReconcileSubmissionStatus(ctx);
    }

    public async Task OnRecalculation(ScoreCommitContext ctx)
    {
        await ReconcileSubmissionStatus(ctx);
    }

    public async Task OnDeletion(ScoreCommitContext ctx)
    {
        var score = ctx.Score;

        score.SubmissionStatus = SubmissionStatus.Deleted;

        await ReconcileSubmissionStatus(ctx);
    }

    public async Task OnRestoration(ScoreCommitContext ctx)
    {
        var score = ctx.Score;

        score.SubmissionStatus = score.IsPassed
            ? SubmissionStatus.Submitted
            : SubmissionStatus.Failed;

        await ReconcileSubmissionStatus(ctx);
    }

    private async Task ReconcileSubmissionStatus(ScoreCommitContext ctx)
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

            var demoteResult = await database.Scores.UpdateScore(sameModsPeer);
            if (demoteResult.IsFailure)
                throw new ApplicationException("Failed to demote previous best score: " + demoteResult.Error);

            return;
        }

        var vacatedBest = ctx.OriginalState.SubmissionStatus == SubmissionStatus.Best
                          && score.SubmissionStatus != SubmissionStatus.Best;

        if (vacatedBest && sameModsPeer != null && sameModsPeer.SubmissionStatus != SubmissionStatus.Best)
        {
            sameModsPeer.SubmissionStatus = SubmissionStatus.Best;

            var promoteResult = await database.Scores.UpdateScore(sameModsPeer);
            if (promoteResult.IsFailure)
                throw new ApplicationException("Failed to promote next-best score: " + promoteResult.Error);
        }
    }
}