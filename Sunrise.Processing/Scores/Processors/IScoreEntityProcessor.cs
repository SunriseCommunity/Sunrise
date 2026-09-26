using Sunrise.Processing.Scores.Pipeline;

namespace Sunrise.Processing.Scores.Processors;

public interface IScoreEntityProcessor
{
    int Priority { get; }

    Task OnNewSubmission(ScoreCommitContext ctx);

    Task OnRecalculation(ScoreCommitContext ctx);

    Task OnDeletion(ScoreCommitContext ctx);

    Task OnRestoration(ScoreCommitContext ctx);
}