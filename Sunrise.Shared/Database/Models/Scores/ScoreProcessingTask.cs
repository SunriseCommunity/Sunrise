using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums.Scores;

namespace Sunrise.Shared.Database.Models.Scores;

[Table("score_processing_task")]
[Index(nameof(Status), nameof(Priority), nameof(NextRetryAt))]
[Index(nameof(Status), nameof(LeaseExpiresAt))]
[Index(nameof(TaskType), nameof(ScoreId))]
[Index(nameof(ScoreSubmissionRequestId))]
public class ScoreProcessingTask
{
    public int Id { get; set; }

    public ScoreTaskType TaskType { get; set; }

    [ForeignKey(nameof(ScoreSubmissionRequestId))]
    public ScoreSubmissionRequest? ScoreSubmissionRequest { get; set; }

    public int? ScoreSubmissionRequestId { get; set; }

    [ForeignKey(nameof(ScoreId))]
    public Score? Score { get; set; }

    public int? ScoreId { get; set; }
    public int Priority { get; set; } = (int)ScoreProcessingPriority.High;
    public ScoreProcessingStatus Status { get; set; } = ScoreProcessingStatus.Pending;
    public DateTime? NextRetryAt { get; set; }
    public int RetryCount { get; set; }
    public ScoreProcessingErrorCode? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ClaimToken { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
