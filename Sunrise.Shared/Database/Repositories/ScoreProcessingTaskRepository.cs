using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;

namespace Sunrise.Shared.Database.Repositories;

public class ScoreProcessingTaskRepository(SunriseDbContext dbContext)
{
    public async Task<bool> TryAcquireQueueLock(CancellationToken ct = default)
    {
        // Keep this connection open for the owner's lifetime; the lock is scoped to the score database.
        await dbContext.Database.OpenConnectionAsync(ct);
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT GET_LOCK(SHA2(CONCAT(DATABASE(), ':score-processing'), 256), 0)";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
    }

    public async Task ReleaseQueueLock()
    {
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT RELEASE_LOCK(SHA2(CONCAT(DATABASE(), ':score-processing'), 256))";
        await command.ExecuteScalarAsync();
        await dbContext.Database.CloseConnectionAsync();
    }

    public async Task<bool> IsQueueLockHeld(CancellationToken ct = default)
    {
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COALESCE(IS_USED_LOCK(SHA2(CONCAT(DATABASE(), ':score-processing'), 256)) = CONNECTION_ID(), 0)";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
    }

    public async Task AddQueueEntry(ScoreProcessingTask task, CancellationToken ct = default)
    {
        dbContext.ScoreProcessingTasks.Add(task);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> TryAddQueueEntry(ScoreProcessingTask task, CancellationToken ct = default)
    {
        try
        {
            dbContext.ScoreProcessingTasks.Add(task);
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsActiveTaskConflict(ex))
        {
            if (dbContext.Entry(task).State == EntityState.Added)
                dbContext.Entry(task).State = EntityState.Detached;

            return false;
        }
    }

    public async Task<List<ScoreProcessingTask>> BulkAddScoreTasks(
        List<int> scoreIds,
        ScoreTaskType taskType,
        ScoreProcessingPriority priority,
        CancellationToken ct = default)
    {
        if (scoreIds.Count == 0)
            return [];

        var existingScoreIds = await dbContext.Scores
            .Where(score => scoreIds.Contains(score.Id))
            .Select(score => score.Id)
            .ToListAsync(ct);

        var alreadyActiveScoreIds = await dbContext.ScoreProcessingTasks
            .Where(task => task.ScoreId != null && scoreIds.Contains(task.ScoreId.Value) && task.TaskType != ScoreTaskType.BeatmapStatusChange)
            .FilterInProgressTasks()
            .Select(task => task.ScoreId!.Value)
            .ToListAsync(ct);

        var skip = alreadyActiveScoreIds.ToHashSet();
        var createdAt = DateTime.UtcNow;

        var tasks = existingScoreIds
            .Where(scoreId => !skip.Contains(scoreId))
            .Select(scoreId => new ScoreProcessingTask
            {
                TaskType = taskType,
                ScoreId = scoreId,
                Priority = (int)priority,
                CreatedAt = createdAt
            })
            .ToList();

        if (tasks.Count == 0)
            return tasks;

        dbContext.ScoreProcessingTasks.AddRange(tasks);
        await dbContext.SaveChangesAsync(ct);

        return tasks;
    }

    public async Task<List<ScoreProcessingTask>> ClaimPendingBatch(int limit, TimeSpan lease, CancellationToken ct = default, IReadOnlyCollection<int>? activeTaskIds = null)
    {
        var claimToken = Guid.NewGuid().ToString("N");
        var leaseUntil = DateTime.UtcNow.Add(lease);
        List<object> parameters = [(int)ScoreProcessingStatus.Pending, (int)ScoreProcessingStatus.Processing, limit,
            (int)ScoreProcessingStatus.Processing, claimToken, leaseUntil];
        var activeExclusion = "";
        if (activeTaskIds is { Count: > 0 })
        {
            var placeholders = Enumerable.Range(parameters.Count, activeTaskIds.Count).Select(index => "{" + index + "}");
            activeExclusion = "AND Id NOT IN (" + string.Join(", ", placeholders) + ")";
            parameters.AddRange(activeTaskIds.Cast<object>());
        }

        await dbContext.Database.ExecuteSqlRawAsync($@"
            UPDATE score_processing_task AS target
            JOIN (
                SELECT Id
                FROM score_processing_task
                WHERE (
                        Status = {{0}}
                        OR (Status = {{1}} AND LeaseExpiresAt < UTC_TIMESTAMP())
                      )
                  AND (NextRetryAt IS NULL OR NextRetryAt <= UTC_TIMESTAMP())
                  {activeExclusion}
                ORDER BY Priority DESC, CreatedAt, Id
                LIMIT {{2}}
            ) AS picked ON picked.Id = target.Id
            SET target.Status = {{3}},
                target.ClaimToken = {{4}},
                target.LeaseExpiresAt = {{5}}",
            parameters,
            ct);

        return await dbContext.ScoreProcessingTasks
            .AsNoTracking()
            .Where(task => task.ClaimToken == claimToken)
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(List<ScoreProcessingTask>, int)> GetTasks(
        QueryOptions? options = null,
        ScoreProcessingStatus? status = null,
        ScoreTaskType? taskType = null,
        int? scoreId = null,
        int? taskId = null,
        CancellationToken ct = default)
    {
        var query = dbContext.ScoreProcessingTasks.AsQueryable();

        if (status != null) query = query.Where(t => t.Status == status);
        if (taskType != null) query = query.Where(t => t.TaskType == taskType);
        if (scoreId != null) query = query.Where(t => t.ScoreId == scoreId);
        if (taskId != null) query = query.Where(t => t.Id == taskId);

        query = query.OrderByDescending(t => t.Id);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await query.CountAsync(ct);

        var tasks = await query
            .UseQueryOptions(options)
            .ToListAsync(ct);

        return (tasks, totalCount);
    }

    public async Task<ScoreProcessingTask?> GetTaskById(int id, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.ScoreProcessingTasks
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<ScoreProcessingTask?> GetActiveTaskByScoreId(int scoreId, CancellationToken ct = default)
    {
        return await dbContext.ScoreProcessingTasks
            .Where(t => t.ScoreId == scoreId)
            .FilterInProgressTasks()
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> TryMarkClaimedForDeletion(int taskId, string claimToken, CancellationToken ct = default)
    {
        var affected = await dbContext.ScoreProcessingTasks
            .Where(task => task.Id == taskId && task.ClaimToken == claimToken)
            .ExecuteDeleteAsync(ct);

        return affected == 1;
    }

    public async Task<int> DeleteBeatmapStatusChanges(int userId, GameMode gameMode, string beatmapHash, int? exceptTaskId, CancellationToken ct = default)
    {
        return await dbContext.ScoreProcessingTasks
            .Where(t => t.TaskType == ScoreTaskType.BeatmapStatusChange && t.Status != ScoreProcessingStatus.Failed && t.Id != exceptTaskId
                        && dbContext.Scores.Any(s => s.Id == t.ScoreId && s.UserId == userId && s.GameMode == gameMode && s.BeatmapHash == beatmapHash))
            .ExecuteDeleteAsync(ct);
    }

    public async Task<bool> TryMarkClaimedAsFailed(int taskId, string claimToken, ScoreProcessingError error, TimeSpan nextRetryDelay, CancellationToken ct = default)
    {
        var isPermanent = error.Disposition == ScoreProcessingDisposition.Permanent;
        var maxRetries = Configuration.ScoreProcessingMaxRetries;
        var nextRetryAt = DateTime.UtcNow + nextRetryDelay;

        var affected = await dbContext.ScoreProcessingTasks
            .Where(t => t.Id == taskId && t.ClaimToken == claimToken)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.RetryCount, t => t.RetryCount + 1)
                    .SetProperty(t => t.ErrorCode, error.Code)
                    .SetProperty(t => t.ErrorMessage, error.Message)
                    .SetProperty(t => t.ClaimToken, (string?)null)
                    .SetProperty(t => t.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(t => t.Status, t => isPermanent || (t.RunId == null && t.TaskType != ScoreTaskType.BeatmapStatusChange && t.RetryCount + 1 >= maxRetries)
                        ? ScoreProcessingStatus.Failed
                        : ScoreProcessingStatus.Pending)
                    .SetProperty(t => t.NextRetryAt, t => isPermanent || (t.RunId == null && t.TaskType != ScoreTaskType.BeatmapStatusChange && t.RetryCount + 1 >= maxRetries)
                        ? null
                        : nextRetryAt),
                ct);

        return affected == 1;
    }

    public async Task<UnitResult<string>> CancelTask(int taskId, CancellationToken ct = default)
    {
        var affected = await dbContext.ScoreProcessingTasks
            .Where(t => t.Id == taskId && t.Status == ScoreProcessingStatus.Pending && t.TaskType != ScoreTaskType.BeatmapStatusChange)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Status, ScoreProcessingStatus.Failed)
                    .SetProperty(t => t.ErrorCode, ScoreProcessingErrorCode.CancelledByOperator)
                    .SetProperty(t => t.ErrorMessage, (string?)"Cancelled by operator")
                    .SetProperty(t => t.NextRetryAt, (DateTime?)null)
                    .SetProperty(t => t.ClaimToken, (string?)null)
                    .SetProperty(t => t.LeaseExpiresAt, (DateTime?)null),
                ct);

        if (affected == 1)
            return UnitResult.Success<string>();

        var task = await dbContext.ScoreProcessingTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);

        if (task == null)
            return UnitResult.Failure($"Score task {taskId} was not found.");

        if (task.Status == ScoreProcessingStatus.Processing)
            return UnitResult.Failure($"Score task {taskId} is currently being processed and cannot be cancelled.");

        if (task.Status == ScoreProcessingStatus.Failed)
            return UnitResult.Failure($"Score task {taskId} has already failed; nothing to cancel.");

        return UnitResult.Failure($"Score task {taskId} could not be cancelled.");
    }

    public async Task<UnitResult<string>> TryRequeueFailedTask(int taskId, CancellationToken ct = default)
    {
        var task = await dbContext.ScoreProcessingTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);

        if (task == null)
            return UnitResult.Failure($"Score task {taskId} was not found.");

        if (task.Status != ScoreProcessingStatus.Failed)
            return UnitResult.Failure($"Score task {taskId} is not in a failed state and cannot be requeued.");

        var activeTaskExists = await dbContext.ScoreProcessingTasks
            .Where(t => t.Id != taskId)
            .FilterInProgressTasks()
            .AnyAsync(t =>
                    (task.ScoreId != null && t.ScoreId == task.ScoreId)
                    || (task.ScoreSubmissionRequestId != null && t.ScoreSubmissionRequestId == task.ScoreSubmissionRequestId),
                ct);

        if (activeTaskExists)
            return UnitResult.Failure($"Score task {taskId} cannot be requeued because the same target already has an active task.");

        try
        {
            var affected = await dbContext.ScoreProcessingTasks
                .Where(t => t.Id == taskId && t.Status == ScoreProcessingStatus.Failed)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(t => t.Status, ScoreProcessingStatus.Pending)
                        .SetProperty(t => t.RetryCount, 0)
                        .SetProperty(t => t.NextRetryAt, (DateTime?)null)
                        .SetProperty(t => t.ClaimToken, (string?)null)
                        .SetProperty(t => t.LeaseExpiresAt, (DateTime?)null)
                        .SetProperty(t => t.ErrorCode, (ScoreProcessingErrorCode?)null)
                        .SetProperty(t => t.ErrorMessage, (string?)null),
                    ct);

            return affected == 1
                ? UnitResult.Success<string>()
                : UnitResult.Failure($"Score task {taskId} could not be requeued.");
        }
        catch (DbUpdateException ex) when (IsActiveTaskConflict(ex))
        {
            return UnitResult.Failure($"Score task {taskId} cannot be requeued because the same target already has an active task.");
        }
    }

    public async Task<int> TryRequeueFailedTasks(IEnumerable<int>? taskIds = null, CancellationToken ct = default)
    {
        List<int> ids;

        if (taskIds == null)
        {
            ids = await dbContext.ScoreProcessingTasks
                .Where(task => task.Status == ScoreProcessingStatus.Failed)
                .Select(task => task.Id)
                .ToListAsync(ct);
        }
        else
        {
            ids = taskIds
                .Distinct()
                .ToList();
        }

        if (ids.Count == 0)
            return 0;

        var requeuedCount = 0;

        foreach (var id in ids)
        {
            if ((await TryRequeueFailedTask(id, ct)).IsSuccess)
                requeuedCount++;
        }

        return requeuedCount;
    }

    public async Task<Dictionary<ScoreProcessingStatus, long>> CountByStatus(CancellationToken ct = default)
    {
        var grouped = await dbContext.ScoreProcessingTasks
            .AsNoTracking()
            .GroupBy(task => task.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.LongCount()
            })
            .ToListAsync(ct);

        return grouped.ToDictionary(group => group.Status, group => group.Count);
    }

    public async Task<int> RefreshClaimLease(int taskId, string claimToken, DateTime leaseUntil, CancellationToken ct = default)
    {
        return await dbContext.ScoreProcessingTasks
            .Where(task => task.Id == taskId && task.ClaimToken == claimToken)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(task => task.LeaseExpiresAt, leaseUntil),
                ct);
    }

    public async Task ReleaseClaim(int taskId, string claimToken, CancellationToken ct = default)
    {
        await dbContext.ScoreProcessingTasks
            .Where(task => task.Id == taskId && task.ClaimToken == claimToken && task.Status == ScoreProcessingStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(task => task.Status, ScoreProcessingStatus.Pending)
                    .SetProperty(task => task.ClaimToken, (string?)null)
                    .SetProperty(task => task.LeaseExpiresAt, (DateTime?)null),
                ct);
    }

    private static bool IsActiveTaskConflict(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;

        return message.Contains("UX_score_processing_task_active_score", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UX_score_processing_task_active_submission_request", StringComparison.OrdinalIgnoreCase);
    }

}
