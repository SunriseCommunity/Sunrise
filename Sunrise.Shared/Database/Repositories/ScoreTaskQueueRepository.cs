using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;

namespace Sunrise.Shared.Database.Repositories;

public class ScoreTaskQueueRepository(SunriseDbContext dbContext)
{
    public async Task AddQueueEntry(ScoreTaskQueue task, CancellationToken ct = default)
    {
        dbContext.ScoreTaskQueue.Add(task);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> TryAddQueueEntry(ScoreTaskQueue task, CancellationToken ct = default)
    {
        try
        {
            dbContext.ScoreTaskQueue.Add(task);
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

    public async Task<List<ScoreTaskQueue>> ClaimPendingBatch(int limit, TimeSpan lease, CancellationToken ct = default)
    {
        var claimToken = Guid.NewGuid().ToString("N");
        var leaseUntil = DateTime.UtcNow.Add(lease);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE score_task_queue AS target
            JOIN (
                SELECT Id
                FROM score_task_queue
                WHERE (
                        Status = {(int)ScoreProcessingStatus.Pending}
                        OR (Status = {(int)ScoreProcessingStatus.Processing} AND LeaseExpiresAt < UTC_TIMESTAMP())
                      )
                  AND (NextRetryAt IS NULL OR NextRetryAt <= UTC_TIMESTAMP())
                ORDER BY Priority DESC, CreatedAt, Id
                LIMIT {limit}
            ) AS picked ON picked.Id = target.Id
            SET target.Status = {(int)ScoreProcessingStatus.Processing},
                target.ClaimToken = {claimToken},
                target.LeaseExpiresAt = {leaseUntil}",
            ct);

        return await dbContext.ScoreTaskQueue
            .AsNoTracking()
            .Where(task => task.ClaimToken == claimToken)
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task MarkForDeletion(int taskId, CancellationToken ct = default)
    {
        await dbContext.ScoreTaskQueue
            .Where(task => task.Id == taskId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task MarkAsFailed(int taskId, ScoreProcessingError error, TimeSpan nextRetryDelay, CancellationToken ct = default)
    {
        var task = await dbContext.ScoreTaskQueue.FindAsync([taskId], ct);
        if (task == null)
            return;

        task.RetryCount++;
        task.ErrorCode = error.Code;
        task.ErrorMessage = error.Message;
        task.ClaimToken = null;
        task.LeaseExpiresAt = null;

        if (error.Disposition == ScoreProcessingDisposition.Permanent || task.RetryCount >= Configuration.ScoreProcessingMaxRetries)
        {
            task.Status = ScoreProcessingStatus.Failed;
            task.NextRetryAt = null;
        }
        else
        {
            task.Status = ScoreProcessingStatus.Pending;
            task.NextRetryAt = DateTime.UtcNow + nextRetryDelay;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<UnitResult<string>> CancelTask(int taskId, CancellationToken ct = default)
    {
        var task = await dbContext.ScoreTaskQueue.FindAsync([taskId], ct);
        if (task == null)
            return UnitResult.Failure($"Score task {taskId} was not found.");

        if (task.Status == ScoreProcessingStatus.Processing)
        {
            return UnitResult.Failure(
                $"Score task {taskId} is currently being processed and cannot be cancelled.");
        }

        if (task.Status == ScoreProcessingStatus.Failed)
            return UnitResult.Failure($"Score task {taskId} has already failed; nothing to cancel.");

        task.Status = ScoreProcessingStatus.Failed;
        task.NextRetryAt = null;
        task.ClaimToken = null;
        task.LeaseExpiresAt = null;
        task.ErrorCode = ScoreProcessingErrorCode.CancelledByOperator;
        task.ErrorMessage = "Cancelled by operator";

        await dbContext.SaveChangesAsync(ct);
        return UnitResult.Success<string>();
    }

    public async Task<bool> TryRequeueFailedTask(int taskId, CancellationToken ct = default)
    {
        var task = await dbContext.ScoreTaskQueue.FindAsync([taskId], ct);
        if (task is not { Status: ScoreProcessingStatus.Failed })
            return false;

        task.Status = ScoreProcessingStatus.Pending;
        task.RetryCount = 0;
        task.NextRetryAt = null;
        task.ClaimToken = null;
        task.LeaseExpiresAt = null;
        task.ErrorCode = null;
        task.ErrorMessage = null;

        try
        {
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsActiveTaskConflict(ex))
        {
            await dbContext.Entry(task).ReloadAsync(ct);
            return false;
        }
    }

    public async Task<int> TryRequeueFailedTasks(IEnumerable<int>? taskIds = null, CancellationToken ct = default)
    {
        List<int> ids;

        if (taskIds == null)
        {
            ids = await dbContext.ScoreTaskQueue
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
            if (await TryRequeueFailedTask(id, ct))
                requeuedCount++;
        }

        return requeuedCount;
    }

    public async Task<Dictionary<ScoreProcessingStatus, long>> CountByStatus(CancellationToken ct = default)
    {
        var grouped = await dbContext.ScoreTaskQueue
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
        return await dbContext.ScoreTaskQueue
            .Where(task => task.Id == taskId && task.ClaimToken == claimToken)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(task => task.LeaseExpiresAt, leaseUntil),
                ct);
    }

    private static bool IsActiveTaskConflict(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;

        return message.Contains("UX_score_task_queue_active_score", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UX_score_task_queue_active_payload", StringComparison.OrdinalIgnoreCase);
    }
}