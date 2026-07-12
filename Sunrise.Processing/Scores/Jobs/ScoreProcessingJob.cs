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
                List<ScoreProcessingTask> claimed;

                using (var claimScope = scopeFactory.CreateScope())
                {
                    var database = claimScope.ServiceProvider.GetRequiredService<DatabaseService>();
                    claimed = await database.ScoreProcessingTasks.ClaimPendingBatch(
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

    private async Task ProcessEntry(ScoreProcessingTask task, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;

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
                if (!await CleanupCompletedTask(bookkeepingDatabase, task, ct))
                {
                    Log.Warning("Skipped completion cleanup for score task {TaskId} ({TaskType}) because its claim was lost", task.Id, task.TaskType);
                    return;
                }

                Log.Information("Successfully processed score task {TaskId} ({TaskType}) for user {UserId}", task.Id, task.TaskType, affectedUserId);
                SunriseMetrics.ScoreProcessingEntryCounterInc("success", task.TaskType);
                return;
            }

            var error = result.Error;
            var isDuplicateScore = error.Code == ScoreProcessingErrorCode.DuplicateScore;

            if (isDuplicateScore && task.TaskType == ScoreTaskType.Submission)
            {
                if (!await CleanupCompletedTask(bookkeepingDatabase, task, ct))
                {
                    Log.Warning("Skipped duplicate submission cleanup for score task {TaskId} because its claim was lost", task.Id);
                    return;
                }

                Log.Information("Cleaned up duplicate submission task {TaskId} for user {UserId}", task.Id, affectedUserId);
                SunriseMetrics.ScoreProcessingEntryCounterInc("success", task.TaskType, error.Code);
                return;
            }

            var claimToken = task.ClaimToken;
            if (string.IsNullOrWhiteSpace(claimToken))
            {
                Log.Warning("Skipped failure bookkeeping for score task {TaskId} ({TaskType}) because it has no claim token", task.Id, task.TaskType);
                return;
            }

            if (!await bookkeepingDatabase.ScoreProcessingTasks.TryMarkClaimedAsFailed(task.Id, claimToken, error, GetBackoffDelay(task.RetryCount), ct))
            {
                Log.Warning("Skipped failure bookkeeping for score task {TaskId} ({TaskType}) because its claim was lost", task.Id, task.TaskType);
                return;
            }

            var isPermanent = error.Disposition == ScoreProcessingDisposition.Permanent;

            Log.Warning("Score processing failed for task {TaskId} ({TaskType}), user {UserId}: [{Code}] {Error}",
                task.Id,
                task.TaskType,
                affectedUserId,
                error.Code,
                error.Message);

            SunriseMetrics.ScoreProcessingEntryCounterInc(
                isPermanent ? "permanent_failure" : "retryable_failure",
                task.TaskType,
                error.Code);

            if (isPermanent && task.TaskType == ScoreTaskType.Submission)
                NotifyUserOfPermanentFailure(sessions, task, affectedUserId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await HandleUnexpectedEntryException(task, affectedUserId, ex);
        }
        finally
        {
            RecordTaskDuration(task.TaskType, startTime);
        }
    }

    private static void NotifyUserOfPermanentFailure(SessionRepository sessions, ScoreProcessingTask task, int? affectedUserId)
    {
        Log.Warning("Score processing permanently failed for submission task {TaskId}, user {UserId}", task.Id, affectedUserId);

        if (affectedUserId.HasValue && sessions.TryGetSession(out var userSession, userId: affectedUserId.Value) && userSession != null)
            userSession.SendNotification($"One of your submitted scores couldn't be processed. If you think this is a mistake, please contact the support with task ID: {task.Id}");
    }

    private static async Task<bool> CleanupCompletedTask(DatabaseService database, ScoreProcessingTask task, CancellationToken ct)
    {
        var claimToken = task.ClaimToken;
        if (string.IsNullOrWhiteSpace(claimToken))
            return false;

        if (task is { TaskType: ScoreTaskType.Submission, ScoreSubmissionRequestId: not null })
        {
            var deletedTask = false;
            var cleanupResult = await database.CommitAsTransactionAsync(async () =>
                {
                    deletedTask = await database.ScoreProcessingTasks.TryMarkClaimedForDeletion(task.Id, claimToken, ct);
                    if (!deletedTask)
                        return;

                    await database.ScoreSubmissionRequests.DeleteById(task.ScoreSubmissionRequestId.Value, ct);
                },
                ct);

            if (cleanupResult.IsFailure)
                throw new ApplicationException($"Failed to clean up completed submission task {task.Id}: {cleanupResult.Error}");

            return deletedTask;
        }

        return await database.ScoreProcessingTasks.TryMarkClaimedForDeletion(task.Id, claimToken, ct);
    }

    private async Task HandleUnexpectedEntryException(ScoreProcessingTask task, int? affectedUserId, Exception ex)
    {
        Log.Error(ex, "Unexpected exception while processing score task {TaskId} ({TaskType}) for user {UserId}", task.Id, task.TaskType, affectedUserId);
        SunriseMetrics.ScoreProcessingEntryCounterInc("unexpected", task.TaskType, ScoreProcessingErrorCode.Unexpected);

        try
        {
            using var failureScope = scopeFactory.CreateScope();
            var failureDatabase = failureScope.ServiceProvider.GetRequiredService<DatabaseService>();
            var unexpectedError = new ScoreProcessingError(ScoreProcessingErrorCode.Unexpected, ex.Message, ScoreProcessingDisposition.Retryable);
            var claimToken = task.ClaimToken;

            if (string.IsNullOrWhiteSpace(claimToken))
            {
                Log.Warning("Skipped unexpected-failure bookkeeping for score task {TaskId} ({TaskType}) because it has no claim token", task.Id, task.TaskType);
                return;
            }

            if (!await failureDatabase.ScoreProcessingTasks.TryMarkClaimedAsFailed(task.Id, claimToken, unexpectedError, GetBackoffDelay(task.RetryCount)))
            {
                Log.Warning("Skipped unexpected-failure bookkeeping for score task {TaskId} ({TaskType}) because its claim was lost", task.Id, task.TaskType);
            }
        }
        catch (Exception markFailedException)
        {
            Log.Error(markFailedException,
                "Failed to mark score task {TaskId} as failed after unexpected exception for user {UserId}",
                task.Id,
                affectedUserId);
        }
    }

    private static async Task<int?> ResolveAffectedUserId(DatabaseService database, ScoreProcessingTask task, CancellationToken ct)
    {
        if (task.ScoreSubmissionRequestId.HasValue)
            return await database.ScoreSubmissionRequests.GetUserIdByPayloadId(task.ScoreSubmissionRequestId.Value, ct);

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

    private static void RecordTaskDuration(ScoreTaskType taskType, DateTime startTime)
    {
        var duration = (DateTime.UtcNow - startTime).TotalSeconds;
        SunriseMetrics.RecordScoreProcessingTaskDuration(duration, taskType);
    }
}