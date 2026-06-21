using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;

namespace Sunrise.Shared.Database.Extensions;

public static class ScoreProcessingTaskQueryExtensions
{
    public static IQueryable<ScoreProcessingTask> FilterInProgressTasks(this IQueryable<ScoreProcessingTask> queryable)
    {
        return queryable.Where(task => task.Status == ScoreProcessingStatus.Pending || task.Status == ScoreProcessingStatus.Processing);
    }
}