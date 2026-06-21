using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Attributes;
using Sunrise.API.Extensions;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.API.Services;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Enums.Scores;

namespace Sunrise.API.Controllers;

[ApiController]
[ApiHttpTrace]
[Route("score-processing")]
[Subdomain("api")]
[Authorize("RequireSuperUser")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status403Forbidden)]
public class ScoreProcessingController(ScoreProcessingService scoreProcessingService) : ControllerBase
{
    [HttpGet("")]
    [EndpointDescription("List score processing tasks (filterable, paginated)")]
    [ProducesResponseType(typeof(ScoreProcessingTasksResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks(
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 25,
        [FromQuery(Name = "status")] ScoreProcessingStatus? status = null,
        [FromQuery(Name = "task_type")] ScoreTaskType? taskType = null,
        [FromQuery(Name = "score_id")] int? scoreId = null,
        [FromQuery(Name = "task_id")] int? taskId = null,
        CancellationToken ct = default)
    {
        return await scoreProcessingService.GetTasks(page, limit, status, taskType, scoreId, taskId, ct);
    }

    [HttpGet("stats")]
    [EndpointDescription("Score processing queue stats (pending/processing/failed counts + raw ETA)")]
    [ProducesResponseType(typeof(ScoreProcessingStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct = default)
    {
        return await scoreProcessingService.GetStats(ct);
    }

    [HttpGet("{id:int}")]
    [EndpointDescription("Get a single score processing task")]
    [ProducesResponseType(typeof(ScoreProcessingTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTask([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        return await scoreProcessingService.GetTask(id, ct);
    }

    [HttpGet("score/{scoreId:int}")]
    [EndpointDescription("Preview a score and its active processing task")]
    [ProducesResponseType(typeof(ScoreProcessingPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreview([Range(1, int.MaxValue)] int scoreId, CancellationToken ct = default)
    {
        return await scoreProcessingService.GetPreview(scoreId, ct);
    }

    [HttpPost("")]
    [EndpointDescription("Queue a recalculate/restore/delete action for a score")]
    [ProducesResponseType(typeof(ScoreProcessingTaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTask([FromBody] CreateScoreProcessingTaskRequest request, CancellationToken ct = default)
    {
        var executorId = HttpContext.GetCurrentUserOrThrow().Id;
        return await scoreProcessingService.CreateTask(executorId, request.ScoreId, request.Action, ct);
    }

    [HttpPost("{id:int}/cancel")]
    [EndpointDescription("Stop a pending score processing task")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelTask([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var executorId = HttpContext.GetCurrentUserOrThrow().Id;
        return await scoreProcessingService.CancelTask(executorId, id, ct);
    }

    [HttpPost("{id:int}/requeue")]
    [EndpointDescription("Requeue a failed score processing task")]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponseType), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequeueTask([Range(1, int.MaxValue)] int id, CancellationToken ct = default)
    {
        var executorId = HttpContext.GetCurrentUserOrThrow().Id;
        return await scoreProcessingService.Requeue(executorId, id, ct);
    }

    [HttpPost("bulk")]
    [EndpointDescription("Queue an action for an explicit list of score ids (max 100)")]
    [ProducesResponseType(typeof(BulkScoreProcessingResultResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkByIds([FromBody] BulkScoreProcessingRequest request, CancellationToken ct = default)
    {
        var executorId = HttpContext.GetCurrentUserOrThrow().Id;
        return await scoreProcessingService.BulkByIds(executorId, request.ScoreIds, request.Action, ct);
    }

    [HttpPost("bulk-by-filter")]
    [EndpointDescription("Queue an action for every score matching the filter for a user (runs in background)")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult BulkByFilter([FromBody] BulkScoreProcessingByFilterRequest request)
    {
        var executorId = HttpContext.GetCurrentUserOrThrow().Id;
        return scoreProcessingService.BulkByFilter(executorId, request);
    }

    [HttpGet("events")]
    [EndpointDescription("List score processing audit events (filterable, paginated)")]
    [ProducesResponseType(typeof(EventScoreProcessingListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [Range(1, int.MaxValue)] [FromQuery(Name = "page")]
        int page = 1,
        [Range(1, 100)] [FromQuery(Name = "limit")]
        int limit = 25,
        [FromQuery(Name = "types")] List<ScoreProcessingEventType>? types = null,
        [FromQuery(Name = "score_id")] int? scoreId = null,
        CancellationToken ct = default)
    {
        return await scoreProcessingService.GetEvents(page, limit, types, scoreId, ct);
    }
}