namespace Sunrise.Shared.Enums.Scores;

public enum ScoreProcessingErrorCode
{
    Unexpected = -1,
    BeatmapNotFound = 1,
    DuplicateScore = 2,
    PpCalculationFailed = 3,
    ReplayMissing = 4,
    InvalidMods = 5,
    NonStandardModsUnsupported = 6,
    BannablePpThreshold = 7,
    InvalidChecksums = 8,
    UserNotFound = 9,
    UserStatsNotFound = 10,
    UserGradesNotFound = 11,
    TransactionFailed = 12,
    ParsedScoreInvalid = 13,
    CancelledByOperator = 14,
    InvalidScoreState = 15
}