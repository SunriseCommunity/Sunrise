using CSharpFunctionalExtensions;
using Sunrise.Shared.Enums.Scores;

namespace Sunrise.Shared.Objects;

public readonly record struct ScoreProcessingError(
    ScoreProcessingErrorCode Code,
    string Message,
    ScoreProcessingDisposition Disposition = ScoreProcessingDisposition.Permanent)
{
    public Result<T, ScoreProcessingError> ToResult<T>()
        => Result.Failure<T, ScoreProcessingError>(this);

    public UnitResult<ScoreProcessingError> ToUnit()
        => UnitResult.Failure(this);
}
