using CSharpFunctionalExtensions;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Processing.Scores.Pipeline;

[TraceExecution]
public class ScoreCommitPipeline
{
    private readonly DatabaseService _database;
    private readonly IScoreEntityProcessor[] _processors;

    public ScoreCommitPipeline(DatabaseService database, IEnumerable<IScoreEntityProcessor> processors)
    {
        _database = database;
        _processors = processors.OrderBy(p => p.Priority).ToArray();
    }

    public async Task<Result> Commit(
        ScoreCommitContext ctx,
        ScoreProcessingTask? task,
        CancellationToken ct)
    {
        return await _database.CommitAsTransactionAsync(async () => { await ExecuteCommitAsync(ctx, task, ct); }, ct);
    }

    private async Task ExecuteCommitAsync(
        ScoreCommitContext ctx,
        ScoreProcessingTask? task,
        CancellationToken ct)
    {
        var score = ctx.Score;

        await _database.Users.Stats.LockAndRefreshUserStats(ctx.UserStats, ct);
        await _database.Users.Grades.LockAndRefreshUserGrades(ctx.UserGrades, ct);

        if (ctx.TaskType != ScoreTaskType.Submission)
        {
            var newPerformancePoints = score.PerformancePoints;
            var locked = await _database.Scores.LockAndRefreshScore(score, ct); // TODO: This will cause the deadlocks, since we are first locking the main score and then peers. I'm partially fine with this since we have retries, but we will need to refactor this system some day for one single lock.
            if (!locked)
                throw new ApplicationException($"Score {score.Id} was not found while locking score commit target");

            score.PerformancePoints = newPerformancePoints;
        }

        score.LocalProperties = new LocalProperties().FromScore(score);

        ctx.OriginalState = ScoreStateSnapshot.Capture(score);

        EnrichScoreWithBeatmapStatus(score, ctx.Beatmap);

        var excludeScoreId = ctx.TaskType == ScoreTaskType.Submission ? (int?)null : score.Id;

        var peers = await _database.Scores.GetUserBeatmapPeersForUpdate(
            score.UserId,
            score.BeatmapHash,
            score.GameMode,
            score.Mods,
            excludeScoreId,
            ct);

        ctx.UserPersonalBestScores = peers;

        foreach (var processor in _processors)
        {
            await DispatchProcessor(processor, ctx);
        }

        var refreshClaimLeaseResult = await TryRefreshClaimLease(task, ct);
        if (refreshClaimLeaseResult.IsFailure)
            throw new ApplicationException(refreshClaimLeaseResult.Error);
    }

    private static void EnrichScoreWithBeatmapStatus(Score score, Beatmap? beatmap)
    {
        var newBeatmapStatus = beatmap?.Status;

        if (!newBeatmapStatus.HasValue || newBeatmapStatus == score.BeatmapStatus)
            return;

        score.BeatmapStatus = newBeatmapStatus.Value;
        score.IsScoreable = newBeatmapStatus.Value.IsScoreable();
        score.LocalProperties = score.LocalProperties.FromScore(score);
    }

    private async Task<UnitResult<string>> TryRefreshClaimLease(ScoreProcessingTask? task, CancellationToken ct)
    {
        if (task == null || string.IsNullOrWhiteSpace(task.ClaimToken))
            return UnitResult.Success<string>();

        var claimToken = task.ClaimToken;
        var leaseUntil = DateTime.UtcNow + Configuration.ScoreProcessingBatchLease;
        var rowsAffected = await _database.ScoreProcessingTasks.RefreshClaimLease(task.Id, claimToken, leaseUntil, ct);

        return rowsAffected == 0
            ? UnitResult.Failure($"Task {task.Id} claim lost; rolling back")
            : UnitResult.Success<string>();
    }

    private static async Task DispatchProcessor(IScoreEntityProcessor processor, ScoreCommitContext ctx)
    {
        switch (ctx.TaskType)
        {
            case ScoreTaskType.Submission:
                await processor.OnNewSubmission(ctx);
                break;
            case ScoreTaskType.Recalculation:
                await processor.OnRecalculation(ctx);
                break;
            case ScoreTaskType.Delete:
                await processor.OnDeletion(ctx);
                break;
            case ScoreTaskType.Restore:
                await processor.OnRestoration(ctx);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ctx.TaskType), ctx.TaskType, $"Unhandled task type: {ctx.TaskType}");
        }
    }
}