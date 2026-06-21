using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using BeatmapStatus = Sunrise.Shared.Enums.Beatmaps.BeatmapStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Shared.Database.Repositories;

public class ScoreProcessingTaskRepository(SunriseDbContext dbContext)
{
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
            .Where(task => task.ScoreId != null
                           && scoreIds.Contains(task.ScoreId.Value)
                           && (task.Status == ScoreProcessingStatus.Pending || task.Status == ScoreProcessingStatus.Processing))
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

    public async Task<List<ScoreProcessingTask>> ClaimPendingBatch(int limit, TimeSpan lease, CancellationToken ct = default)
    {
        var claimToken = Guid.NewGuid().ToString("N");
        var leaseUntil = DateTime.UtcNow.Add(lease);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE score_processing_task AS target
            JOIN (
                SELECT Id
                FROM score_processing_task
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

        return await dbContext.ScoreProcessingTasks
            .AsNoTracking()
            .Where(task => task.ClaimToken == claimToken)
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(List<ScoreProcessingTask>, int)> GetExistingScoreTasks(
        QueryOptions? options = null,
        ScoreProcessingStatus? status = null,
        ScoreTaskType? taskType = null,
        int? scoreId = null,
        GameMode? mode = null,
        Mods? mods = null,
        SubmissionStatus? submissionStatus = null,
        BeatmapStatus? beatmapStatus = null,
        CancellationToken ct = default)
    {
        var query = dbContext.ScoreProcessingTasks.Where(t => t.ScoreId != null);

        if (status != null) query = query.Where(t => t.Status == status);
        if (taskType != null) query = query.Where(t => t.TaskType == taskType);
        if (scoreId != null) query = query.Where(t => t.ScoreId == scoreId);
        if (mode != null) query = query.Where(t => t.Score!.GameMode == mode);
        if (submissionStatus != null) query = query.Where(t => t.Score!.SubmissionStatus == submissionStatus);
        if (beatmapStatus != null) query = query.Where(t => t.Score!.BeatmapStatus == beatmapStatus);
        if (mods != null) query = query.Where(t => t.Score!.Mods == EF.Constant(mods.Value));

        query = query.OrderByDescending(t => t.Id);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await query.CountAsync(ct);

        var tasks = await query
            .UseQueryOptions(options)
            .ToListAsync(ct);

        return (tasks, totalCount);
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

    public async Task MarkForDeletion(int taskId, CancellationToken ct = default)
    {
        await dbContext.ScoreProcessingTasks
            .Where(task => task.Id == taskId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task MarkAsFailed(int taskId, ScoreProcessingError error, TimeSpan nextRetryDelay, CancellationToken ct = default)
    {
        var task = await dbContext.ScoreProcessingTasks.FindAsync([taskId], ct);
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
        var affected = await dbContext.ScoreProcessingTasks
            .Where(t => t.Id == taskId && t.Status == ScoreProcessingStatus.Pending)
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

    public async Task<bool> TryRequeueFailedTask(int taskId, CancellationToken ct = default)
    {
        var task = await dbContext.ScoreProcessingTasks.FindAsync([taskId], ct);
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
            if (await TryRequeueFailedTask(id, ct))
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

    private static bool IsActiveTaskConflict(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;

        return message.Contains("UX_score_processing_task_active_score", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UX_score_processing_task_active_submission_request", StringComparison.OrdinalIgnoreCase);
    }
}