using System.Net;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sunrise.API.Extensions;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Jobs;
using Sunrise.Shared.Repositories;

namespace Sunrise.API.Services;

public class ScoreProcessingService(DatabaseService database, SessionRepository sessions)
{
    private const int MaxBulkIds = 100;

    private static readonly ScoreTaskType[] AllowedActions = [ScoreTaskType.Recalculation, ScoreTaskType.Restore, ScoreTaskType.Delete];

    public async Task<IActionResult> GetTasks(
        int page,
        int limit,
        ScoreProcessingStatus? status,
        ScoreTaskType? taskType,
        int? scoreId,
        int? taskId,
        CancellationToken ct = default)
    {
        var (tasks, totalCount) = await database.ScoreProcessingTasks.GetTasks(
            new QueryOptions(true, new Pagination(page, limit))
            {
                QueryModifier = q => q.Cast<ScoreProcessingTask>()
                    .Include(task => task.Score)
                    .Include(task => task.Score!.User)
            },
            status,
            taskType,
            scoreId,
            taskId,
            ct);

        var parsed = tasks.Select(task => new ScoreProcessingTaskResponse(sessions, task)).ToList();

        return new OkObjectResult(new ScoreProcessingTasksResponse(parsed, totalCount));
    }

    public async Task<IActionResult> GetTask(int taskId, CancellationToken ct = default)
    {
        var task = await database.ScoreProcessingTasks.GetTaskById(taskId,
            new QueryOptions(true)
            {
                QueryModifier = query => query.Cast<ScoreProcessingTask>()
                    .Include(t => t.Score)
                    .Include(t => t.Score!.User)
            },
            ct);

        if (task == null)
            return ApiErrorResponse.Detail.ScoreTaskNotFound.ToProblemResult(HttpStatusCode.NotFound);

        return new OkObjectResult(new ScoreProcessingTaskResponse(sessions, task));
    }

    public async Task<IActionResult> GetPreview(int scoreId, CancellationToken ct = default)
    {
        var score = await database.Scores.GetScore(scoreId,
            new QueryOptions(true)
            {
                QueryModifier = query => query.Cast<Score>().IncludeUser()
            },
            false,
            ct);

        if (score == null)
            return ApiErrorResponse.Detail.ScoreNotFound.ToProblemResult(HttpStatusCode.NotFound);

        var activeTask = await database.ScoreProcessingTasks.GetActiveTaskByScoreId(scoreId, ct);

        var preview = new ScoreProcessingPreviewResponse(
            new AdminScoreResponse(sessions, score),
            activeTask != null ? new ScoreProcessingTaskResponse(sessions, activeTask) : null);

        return new OkObjectResult(preview);
    }

    public async Task<IActionResult> CreateTask(int executorId, int scoreId, ScoreTaskType action, CancellationToken ct = default)
    {
        if (!AllowedActions.Contains(action))
            return ApiErrorResponse.Detail.InvalidScoreProcessingAction.ToProblemResult(HttpStatusCode.BadRequest, ApiErrorResponse.Title.UnableToQueueScoreProcessing);

        var score = await database.Scores.GetScore(scoreId, filterValidScores: false, ct: ct);

        if (score == null)
            return ApiErrorResponse.Detail.ScoreNotFound.ToProblemResult(HttpStatusCode.NotFound);

        var task = new ScoreProcessingTask
        {
            TaskType = action,
            ScoreId = score.Id,
            Priority = (int)ScoreProcessingPriority.Normal,
            CreatedAt = DateTime.UtcNow
        };

        var queued = await database.ScoreProcessingTasks.TryAddQueueEntry(task, ct);

        if (!queued)
            return ApiErrorResponse.Detail.ScoreAlreadyQueued.ToProblemResult(HttpStatusCode.Conflict, ApiErrorResponse.Title.UnableToQueueScoreProcessing);

        await database.Events.ScoreProcessing.AddActionRequestedEvent(executorId, score.Id, task.Id, action, task.Priority, ct);

        var created = await database.ScoreProcessingTasks.GetTaskById(task.Id,
            new QueryOptions(true)
            {
                QueryModifier = query => query.Cast<ScoreProcessingTask>()
                    .Include(t => t.Score)
                    .Include(t => t.Score!.User)
            },
            ct);

        if (created == null)
            return ApiErrorResponse.Detail.ScoreTaskNotFound.ToProblemResult(HttpStatusCode.NotFound);

        return new ObjectResult(new ScoreProcessingTaskResponse(sessions, created))
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    public async Task<IActionResult> CancelTask(int executorId, int taskId, CancellationToken ct = default)
    {
        var task = await database.ScoreProcessingTasks.GetTaskById(taskId, ct: ct);

        if (task == null)
            return ApiErrorResponse.Detail.ScoreTaskNotFound.ToProblemResult(HttpStatusCode.NotFound);

        var result = await database.ScoreProcessingTasks.CancelTask(taskId, ct);

        if (result.IsFailure)
            return result.Error.ToProblemResult(HttpStatusCode.Conflict, ApiErrorResponse.Title.UnableToCancelScoreTask);

        await database.Events.ScoreProcessing.AddCancelledEvent(executorId, taskId, task.ScoreId, ct);

        return new OkResult();
    }

    public async Task<IActionResult> Requeue(int executorId, int taskId, CancellationToken ct = default)
    {
        var task = await database.ScoreProcessingTasks.GetTaskById(taskId, ct: ct);

        if (task == null)
            return ApiErrorResponse.Detail.ScoreTaskNotFound.ToProblemResult(HttpStatusCode.NotFound);

        var priorErrorCode = task.ErrorCode;
        var priorErrorMessage = task.ErrorMessage;

        var requeued = await database.ScoreProcessingTasks.TryRequeueFailedTask(taskId, ct);

        if (!requeued)
            return $"Score task {taskId} is not in a failed state and cannot be requeued.".ToProblemResult(HttpStatusCode.Conflict, ApiErrorResponse.Title.UnableToRequeueScoreTask);

        await database.Events.ScoreProcessing.AddRequeuedEvent(executorId, taskId, task.ScoreId, priorErrorCode, priorErrorMessage, ct);

        return new OkResult();
    }

    public async Task<IActionResult> BulkByIds(int executorId, List<int> scoreIds, ScoreTaskType action, CancellationToken ct = default)
    {
        if (!AllowedActions.Contains(action))
            return ApiErrorResponse.Detail.InvalidScoreProcessingAction.ToProblemResult(HttpStatusCode.BadRequest, ApiErrorResponse.Title.UnableToQueueScoreProcessing);

        var distinctIds = scoreIds.Distinct().ToList();

        if (distinctIds.Count == 0)
            return ApiErrorResponse.Detail.InvalidQueryParameters.ToProblemResult(HttpStatusCode.BadRequest, ApiErrorResponse.Title.UnableToQueueScoreProcessing);

        if (distinctIds.Count > MaxBulkIds)
            return ApiErrorResponse.Detail.TooManyScoreIds.ToProblemResult(HttpStatusCode.BadRequest, ApiErrorResponse.Title.UnableToQueueScoreProcessing);

        var queuedTasks = await database.ScoreProcessingTasks.BulkAddScoreTasks(distinctIds, action, ScoreProcessingPriority.Normal, ct);
        await database.Events.ScoreProcessing.AddActionRequestedEvents(executorId, queuedTasks, action, ct);

        return new OkObjectResult(new BulkScoreProcessingResultResponse(queuedTasks.Count, distinctIds.Count - queuedTasks.Count));
    }

    public IActionResult BulkByFilter(int? executorId, BulkScoreProcessingByFilterRequest request)
    {
        if (!AllowedActions.Contains(request.Action))
            return ApiErrorResponse.Detail.InvalidScoreProcessingAction.ToProblemResult(HttpStatusCode.BadRequest, ApiErrorResponse.Title.UnableToQueueScoreProcessing);

        BackgroundJob.Enqueue<BulkScoreProcessingJob>(service => service.EnqueueByFilter(
            executorId,
            request.Action,
            request.UserId,
            request.Mode,
            request.Mods,
            request.SubmissionStatus,
            request.BeatmapStatus,
            request.SubmittedFrom,
            request.SubmittedTo,
            CancellationToken.None));

        return new OkResult();
    }

    public async Task<IActionResult> GetStats(CancellationToken ct = default)
    {
        var counts = await database.ScoreProcessingTasks.CountByStatus(ct);

        var pending = counts.GetValueOrDefault(ScoreProcessingStatus.Pending);
        var processing = counts.GetValueOrDefault(ScoreProcessingStatus.Processing);
        var failed = counts.GetValueOrDefault(ScoreProcessingStatus.Failed);

        return new OkObjectResult(new ScoreProcessingStatsResponse(pending, processing, failed, EstimatePendingCompletionSeconds(pending)));
    }

    public async Task<IActionResult> GetEvents(int page, int limit, List<ScoreProcessingEventType>? types, int? scoreId, CancellationToken ct = default)
    {
        var (events, totalCount) = await database.Events.ScoreProcessing.GetEvents(
            new QueryOptions(true, new Pagination(page, limit)),
            types,
            scoreId,
            ct);

        var parsed = events.Select(scoreProcessingEvent => new EventScoreProcessingResponse(sessions, scoreProcessingEvent)).ToList();

        return new OkObjectResult(new EventScoreProcessingListResponse(parsed, totalCount));
    }

    private static double? EstimatePendingCompletionSeconds(long pending)
    {
        if (pending <= 0)
            return null;

        var concurrency = Math.Max(1, Configuration.ScoreProcessingMaxConcurrency);
        var batches = Math.Ceiling(pending / (double)concurrency);

        var avgTaskDuration = SunriseMetrics.GetEstimatedAverageTaskDurationSeconds();
        var secondsPerTask = avgTaskDuration > 0 ? avgTaskDuration : Configuration.ScoreProcessingTimeoutSeconds;
        var secondsPerBatch = secondsPerTask + Configuration.ScoreProcessingPollerInterBatchDelaySeconds;

        return batches * secondsPerBatch;
    }
}