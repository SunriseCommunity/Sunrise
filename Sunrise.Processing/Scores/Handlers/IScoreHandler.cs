using CSharpFunctionalExtensions;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Objects;

namespace Sunrise.Processing.Scores.Handlers;

public interface IScoreHandler
{
    Task<UnitResult<ScoreProcessingError>> ExecuteAsync(ScoreTaskQueue task, CancellationToken ct);
}