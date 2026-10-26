namespace Sunrise.Shared.Enums.Scores;

public enum ScoreProcessingEventType
{
    RecalculationRequested = 0,
    RestoreRequested = 1,
    DeleteRequested = 2,
    SubmissionEnqueued = 3,
    Cancelled = 4,
    Requeued = 5,
    BulkRequested = 6
}
