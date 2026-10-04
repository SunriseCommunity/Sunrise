using CSharpFunctionalExtensions;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;

namespace Sunrise.Processing.Utils;

public static class ReplayValidationUtil
{
    private const int HeaderSize = 13;

    public static UnitResult<ScoreProcessingError> ValidateHeader(byte[] replay)
    {
        if (replay.Length < HeaderSize)
            return Failure("Replay is smaller than the replay header");

        return UnitResult.Success<ScoreProcessingError>();
    }

    private static UnitResult<ScoreProcessingError> Failure(string message)
    {
        return new ScoreProcessingError(ScoreProcessingErrorCode.InvalidReplay, message).ToUnit();
    }
}