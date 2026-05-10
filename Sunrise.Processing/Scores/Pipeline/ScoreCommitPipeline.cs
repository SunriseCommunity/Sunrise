using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
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
        ScoreTaskQueue? task,
        CancellationToken ct)
    {
        return await _database.CommitAsTransactionAsync(async () => { await ExecuteCommitAsync(ctx, task, ct); }, ct);
    }

    private async Task ExecuteCommitAsync(
        ScoreCommitContext ctx,
        ScoreTaskQueue? task,
        CancellationToken ct)
    {
        var score = ctx.Score;

        ctx.OriginalState = ScoreStateSnapshot.Capture(score);

        EnrichScoreWithBeatmapStatus(score, ctx.Beatmap);

        var excludeScoreId = ctx.TaskType == ScoreTaskType.Submission ? (int?)null : score.Id;

        var peers = await _database.Scores.GetUserBeatmapPeersForUpdate(
            score.UserId,
            score.BeatmapHash,
            score.Mods,
            excludeScoreId,
            ct);

        ctx.UserPersonalBestScores = peers;

        foreach (var processor in _processors)
        {
            await DispatchProcessor(processor, ctx);
        }

        var persistScoreResult = ctx.TaskType == ScoreTaskType.Submission
            ? await _database.Scores.AddScore(score)
            : await _database.Scores.UpdateScore(score);

        if (persistScoreResult.IsFailure)
            throw new ApplicationException("Failed to persist score: " + persistScoreResult.Error);

        var updateUserStatsResult = await _database.Users.Stats.UpdateUserStats(ctx.UserStats, ctx.User);
        if (updateUserStatsResult.IsFailure)
            throw new ApplicationException("Failed to persist user stats: " + updateUserStatsResult.Error);

        var updateUserGradesResult = await _database.Users.Grades.UpdateUserGrades(ctx.UserGrades);
        if (updateUserGradesResult.IsFailure)
            throw new ApplicationException("Failed to persist user grades: " + updateUserGradesResult.Error);

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

    private async Task<UnitResult<string>> TryRefreshClaimLease(ScoreTaskQueue? task, CancellationToken ct)
    {
        if (task == null || string.IsNullOrWhiteSpace(task.ClaimToken))
            return UnitResult.Success<string>();

        var claimToken = task.ClaimToken;
        var leaseUntil = DateTime.UtcNow + Configuration.ScoreProcessingBatchLease;
        var rowsAffected = await _database.ScoreTaskQueue.RefreshClaimLease(task.Id, claimToken, leaseUntil, ct);

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