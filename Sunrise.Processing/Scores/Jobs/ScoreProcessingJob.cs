using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Repositories;

namespace Sunrise.Processing.Scores.Jobs;

public class ScoreProcessingJob(IServiceScopeFactory scopeFactory)
{
    private const int DefaultBackoffMinutes = 1;

    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ProcessQueue(CancellationToken ct)
    {
        var runStart = DateTime.UtcNow;
        var totalProcessed = 0;
        var outcome = "drained";

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(55));
        var token = timeoutCts.Token;

        Log.Information("Score queue poller tick starting (max concurrency {MaxConcurrency}, batch lease {Lease}s)",
            Configuration.ScoreProcessingMaxConcurrency,
            Configuration.ScoreProcessingBatchLeaseSeconds);

        try
        {
            while (!token.IsCancellationRequested)
            {
                List<ScoreTaskQueue> claimed;

                using (var claimScope = scopeFactory.CreateScope())
                {
                    var database = claimScope.ServiceProvider.GetRequiredService<DatabaseService>();
                    claimed = await database.ScoreTaskQueue.ClaimPendingBatch(
                        Configuration.ScoreProcessingMaxConcurrency,
                        Configuration.ScoreProcessingBatchLease,
                        token);
                }

                if (claimed.Count == 0)
                {
                    if (totalProcessed == 0)
                        outcome = "empty";
                    break;
                }

                Log.Information("Processing batch of {Count} queued score entries", claimed.Count);

                await Parallel.ForEachAsync(claimed,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Configuration.ScoreProcessingMaxConcurrency,
                        CancellationToken = token
                    },
                    async (entry, innerCt) => await ProcessEntry(entry, innerCt));

                totalProcessed += claimed.Count;

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Configuration.ScoreProcessingPollerInterBatchDelaySeconds), token);
                }
                catch (OperationCanceledException)
                {
                    outcome = "cancelled";
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            outcome = "error";
            Log.Error(ex, "Score queue poller tick failed unexpectedly");
            throw;
        }
        finally
        {
            var elapsed = (DateTime.UtcNow - runStart).TotalMilliseconds;
            SunriseMetrics.ScoreProcessingPollerRunCounterInc(outcome, totalProcessed);
            Log.Information("Score queue poller tick finished: outcome={Outcome}, processed={Processed}, elapsed_ms={ElapsedMs}",
                outcome,
                totalProcessed,
                (long)elapsed);
        }
    }

    private async Task ProcessEntry(ScoreTaskQueue task, CancellationToken ct)
    {
        using var entryScope = scopeFactory.CreateScope();
        var entryDatabase = entryScope.ServiceProvider.GetRequiredService<DatabaseService>();
        int? affectedUserId = null;

        try
        {
            var handler = entryScope.ServiceProvider.GetRequiredKeyedService<IScoreHandler>(task.TaskType);
            var sessions = entryScope.ServiceProvider.GetRequiredService<SessionRepository>();
            affectedUserId = await ResolveAffectedUserId(entryDatabase, task, ct);
            var result = await handler.ExecuteAsync(task, ct);

            using var bookkeepingScope = scopeFactory.CreateScope();
            var bookkeepingDatabase = bookkeepingScope.ServiceProvider.GetRequiredService<DatabaseService>();

            if (result.IsSuccess)
            {
                await CleanupCompletedTask(bookkeepingDatabase, task, ct);
                Log.Information("Successfully processed score task {TaskId} ({TaskType}) for user {UserId}", task.Id, task.TaskType, affectedUserId);
                SunriseMetrics.ScoreProcessingEntryCounterInc("success", task.TaskType);
                return;
            }

            var error = result.Error;

            if (task.TaskType == ScoreTaskType.Submission && error.Code == ScoreProcessingErrorCode.DuplicateScore)
            {
                await CleanupCompletedTask(bookkeepingDatabase, task, ct);
                Log.Information("Cleaned up duplicate submission task {TaskId} for user {UserId}", task.Id, affectedUserId);
                SunriseMetrics.ScoreProcessingEntryCounterInc("success", task.TaskType, error.Code);
                return;
            }

            await bookkeepingDatabase.ScoreTaskQueue.MarkAsFailed(task.Id, error, GetBackoffDelay(task.RetryCount), ct);

            Log.Warning("Score processing failed for task {TaskId} ({TaskType}), user {UserId}: [{Code}] {Error}",
                task.Id,
                task.TaskType,
                affectedUserId,
                error.Code,
                error.Message);

            SunriseMetrics.ScoreProcessingEntryCounterInc(
                error.Disposition == ScoreProcessingDisposition.Permanent ? "permanent_failure" : "retryable_failure",
                task.TaskType,
                error.Code);

            if (error.Disposition == ScoreProcessingDisposition.Permanent && task.TaskType == ScoreTaskType.Submission)
            {
                Log.Warning("Score processing permanently failed for submission task {TaskId}, user {UserId}", task.Id, affectedUserId);

                if (affectedUserId.HasValue && sessions.TryGetSession(out var userSession, userId: affectedUserId.Value) && userSession != null)
                    userSession.SendNotification($"One of your submitted scores couldn't be processed. If you think this is a mistake, please contact the support with task ID: {task.Id}");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await HandleUnexpectedEntryException(task, affectedUserId, ex);
        }
    }

    private static async Task CleanupCompletedTask(DatabaseService database, ScoreTaskQueue task, CancellationToken ct)
    {
        if (task is { TaskType: ScoreTaskType.Submission, ScoreProcessingQueueId: not null })
        {
            var cleanupResult = await database.CommitAsTransactionAsync(async () =>
                {
                    await database.ScoreTaskQueue.MarkForDeletion(task.Id, ct);
                    await database.ScoreProcessingQueue.DeleteById(task.ScoreProcessingQueueId.Value, ct);
                },
                ct);

            if (cleanupResult.IsFailure)
                throw new ApplicationException($"Failed to clean up completed submission task {task.Id}: {cleanupResult.Error}");

            return;
        }

        await database.ScoreTaskQueue.MarkForDeletion(task.Id, ct);
    }

    private async Task HandleUnexpectedEntryException(ScoreTaskQueue task, int? affectedUserId, Exception ex)
    {
        Log.Error(ex, "Unexpected exception while processing score task {TaskId} ({TaskType}) for user {UserId}", task.Id, task.TaskType, affectedUserId);
        SunriseMetrics.ScoreProcessingEntryCounterInc("unexpected", task.TaskType, ScoreProcessingErrorCode.Unexpected);

        try
        {
            using var failureScope = scopeFactory.CreateScope();
            var failureDatabase = failureScope.ServiceProvider.GetRequiredService<DatabaseService>();
            var unexpectedError = new ScoreProcessingError(ScoreProcessingErrorCode.Unexpected, ex.Message, ScoreProcessingDisposition.Retryable);

            await failureDatabase.ScoreTaskQueue.MarkAsFailed(task.Id, unexpectedError, GetBackoffDelay(task.RetryCount));
        }
        catch (Exception markFailedException)
        {
            Log.Error(markFailedException,
                "Failed to mark score task {TaskId} as failed after unexpected exception for user {UserId}",
                task.Id,
                affectedUserId);
        }
    }

    private static async Task<int?> ResolveAffectedUserId(DatabaseService database, ScoreTaskQueue task, CancellationToken ct)
    {
        if (task.ScoreProcessingQueueId.HasValue)
            return await database.ScoreProcessingQueue.GetUserIdByPayloadId(task.ScoreProcessingQueueId.Value, ct);

        if (task.ScoreId.HasValue)
            return await database.Scores.GetUserIdByScoreId(task.ScoreId.Value, ct);

        return null;
    }

    private static TimeSpan GetBackoffDelay(int retryCount)
    {
        var schedule = Configuration.ScoreProcessingBackoffSchedule;
        if (schedule.Length == 0)
            return TimeSpan.FromMinutes(DefaultBackoffMinutes);

        var index = Math.Min(retryCount, schedule.Length - 1);
        return schedule[index];
    }
}