using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;

namespace Sunrise.Shared.Objects;

public class UserPersonalBestScores(Score bestScoreByScoreValue, Score? bestScoreBasedByPerformancePoints = null)
{
    public Score BestScoreByScoreValue { get; } = bestScoreByScoreValue;
    public Score BestScoreForPerformanceCalculation { get; } = Configuration.UseNewPerformanceCalculationAlgorithm ? bestScoreBasedByPerformancePoints ?? bestScoreByScoreValue : bestScoreByScoreValue;
}