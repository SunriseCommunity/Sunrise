using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;

namespace Sunrise.Shared.Objects;

public class UserPersonalBestScores(Score bestScoreBasedByTotalScore, Score? bestScoreBasedByPerformancePoints = null)
{
    public Score BestScoreBasedByTotalScore { get; } = bestScoreBasedByTotalScore;
    public Score BestScoreForPerformanceCalculation { get; } = Configuration.UseNewPerformanceCalculationAlgorithm ? bestScoreBasedByPerformancePoints ?? bestScoreBasedByTotalScore : bestScoreBasedByTotalScore;
}