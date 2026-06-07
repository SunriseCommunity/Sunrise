namespace Sunrise.Shared.Enums.Scores;

public enum ScoreProcessingErrorCode
{
    Unexpected = 0,
    BeatmapNotFound = 1,
    DuplicateScore = 2,
    PpCalculationFailed = 3,
    ReplayMissing = 4,
    InvalidMods = 5,
    BannablePpThreshold = 6,
    InvalidChecksums = 7,
    UserNotFound = 8,
    UserStatsNotFound = 9,
    UserGradesNotFound = 10,
    TransactionFailed = 11,
    ParsedScoreInvalid = 12,
    CancelledByOperator = 13,
    InvalidScoreState = 14
}