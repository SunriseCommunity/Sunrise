using Sunrise.Processing.Scores.Pipeline;

namespace Sunrise.Processing.Scores.Processors;

public abstract class ScoreEntityProcessorBase : IScoreEntityProcessor
{
    public abstract int Priority { get; }

    public async Task OnNewSubmission(ScoreCommitContext ctx)
    {
        await Execute(ctx, OnNewSubmissionInternal);
    }

    public async Task OnRecalculation(ScoreCommitContext ctx)
    {
        await Execute(ctx, OnRecalculationInternal);
    }

    public async Task OnDeletion(ScoreCommitContext ctx)
    {
        await Execute(ctx, OnDeletionInternal);
    }

    public async Task OnRestoration(ScoreCommitContext ctx)
    {
        await Execute(ctx, OnRestorationInternal);
    }

    protected virtual Task OnNewSubmissionInternal(ScoreCommitContext ctx)
    {
        throw new InvalidOperationException("OnNewSubmission must be implemented for new submission processing.");
    }

    protected virtual Task OnRecalculationInternal(ScoreCommitContext ctx)
    {
        throw new InvalidOperationException("OnRecalculation must be implemented for new submission processing.");
    }

    protected virtual Task OnDeletionInternal(ScoreCommitContext ctx)
    {
        throw new InvalidOperationException("OnDeletion must be implemented for new submission processing.");
    }

    protected virtual Task OnRestorationInternal(ScoreCommitContext ctx)
    {
        throw new InvalidOperationException("OnRestoration must be implemented for new submission processing.");
    }

    protected virtual Task AfterExecution(ScoreCommitContext ctx)
    {
        return Task.CompletedTask;
    }

    private async Task Execute(ScoreCommitContext ctx, Func<ScoreCommitContext, Task> action)
    {
        await action(ctx);
        await AfterExecution(ctx);
    }
}