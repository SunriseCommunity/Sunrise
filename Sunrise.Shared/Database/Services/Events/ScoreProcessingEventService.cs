using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Events;

public class ScoreProcessingEventService(SunriseDbContext dbContext)
{
    public async Task<Result> AddActionRequestedEvent(int? executorId, int scoreId, int? taskId, ScoreTaskType action, int priority, CancellationToken ct = default)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var newEvent = new EventScoreProcessing
            {
                ExecutorId = executorId,
                ScoreId = scoreId,
                TaskId = taskId,
                EventType = ToRequestedEventType(action)
            };

            newEvent.SetData(new
            {
                Action = action,
                Priority = priority
            });

            dbContext.EventScoreProcessings.Add(newEvent);
            await dbContext.SaveChangesAsync(ct);
        });
    }

    public async Task<Result> AddActionRequestedEvents(int? executorId, IReadOnlyCollection<ScoreProcessingTask> tasks, ScoreTaskType action,
        CancellationToken ct = default)
    {
        if (tasks.Count == 0)
            return Result.Success();

        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var events = tasks.Select(task =>
            {
                var newEvent = new EventScoreProcessing
                {
                    ExecutorId = executorId,
                    ScoreId = task.ScoreId,
                    TaskId = task.Id,
                    EventType = ToRequestedEventType(action)
                };

                newEvent.SetData(new
                {
                    Action = action,
                    task.Priority
                });

                return newEvent;
            }).ToList();

            dbContext.EventScoreProcessings.AddRange(events);
            await dbContext.SaveChangesAsync(ct);
        });
    }

    public async Task<Result> AddCancelledEvent(int? executorId, int taskId, int? scoreId, CancellationToken ct = default)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var newEvent = new EventScoreProcessing
            {
                ExecutorId = executorId,
                ScoreId = scoreId,
                TaskId = taskId,
                EventType = ScoreProcessingEventType.Cancelled
            };

            newEvent.SetData(new
            {
            });

            dbContext.EventScoreProcessings.Add(newEvent);
            await dbContext.SaveChangesAsync(ct);
        });
    }

    public async Task<Result> AddRequeuedEvent(int? executorId, int taskId, int? scoreId, ScoreProcessingErrorCode? priorErrorCode, string? priorErrorMessage,
        CancellationToken ct = default)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var newEvent = new EventScoreProcessing
            {
                ExecutorId = executorId,
                ScoreId = scoreId,
                TaskId = taskId,
                EventType = ScoreProcessingEventType.Requeued
            };

            newEvent.SetData(new
            {
                PriorErrorCode = priorErrorCode,
                PriorErrorMessage = priorErrorMessage
            });

            dbContext.EventScoreProcessings.Add(newEvent);
            await dbContext.SaveChangesAsync(ct);
        });
    }

    public async Task<Result> AddBulkRequestedEvent(int? executorId, ScoreTaskType action, object filterSummary, int matched, int queued, int skipped,
        CancellationToken ct = default)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var newEvent = new EventScoreProcessing
            {
                ExecutorId = executorId,
                EventType = ScoreProcessingEventType.BulkRequested
            };

            newEvent.SetData(new
            {
                Action = action,
                Filters = filterSummary,
                Matched = matched,
                Queued = queued,
                Skipped = skipped
            });

            dbContext.EventScoreProcessings.Add(newEvent);
            await dbContext.SaveChangesAsync(ct);
        });
    }

    public async Task<Result> AddSubmissionEnqueuedEvent(int? scoreSubmissionRequestId, int? submitterUserId, int? taskId, CancellationToken ct = default)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var newEvent = new EventScoreProcessing
            {
                ExecutorId = null,
                TaskId = taskId,
                EventType = ScoreProcessingEventType.SubmissionEnqueued
            };

            newEvent.SetData(new
            {
                ScoreSubmissionRequestId = scoreSubmissionRequestId,
                SubmitterUserId = submitterUserId
            });

            dbContext.EventScoreProcessings.Add(newEvent);
            await dbContext.SaveChangesAsync(ct);
        });
    }

    public async Task<(List<EventScoreProcessing>, int)> GetEvents(QueryOptions? options = null, List<ScoreProcessingEventType>? types = null, int? scoreId = null,
        CancellationToken ct = default)
    {
        var query = dbContext.EventScoreProcessings.AsQueryable();

        if (types is { Count: > 0 })
            query = query.Where(e => types.Contains(e.EventType));

        if (scoreId.HasValue)
            query = query.Where(e => e.ScoreId == scoreId.Value);

        query = query.OrderByDescending(e => e.Id);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await query.CountAsync(ct);

        var events = await query
            .Include(e => e.Executor)
            .UseQueryOptions(options)
            .ToListAsync(ct);

        return (events, totalCount);
    }

    private static ScoreProcessingEventType ToRequestedEventType(ScoreTaskType action)
    {
        return action switch
        {
            ScoreTaskType.Recalculation => ScoreProcessingEventType.RecalculationRequested,
            ScoreTaskType.Restore => ScoreProcessingEventType.RestoreRequested,
            ScoreTaskType.Delete => ScoreProcessingEventType.DeleteRequested,
            ScoreTaskType.Submission => ScoreProcessingEventType.SubmissionEnqueued,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported score processing action for an audit event.")
        };
    }
}