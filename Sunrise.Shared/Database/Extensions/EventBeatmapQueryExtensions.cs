using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Beatmaps;

namespace Sunrise.Shared.Database.Extensions;

public static class EventBeatmapQueryableExtensions
{
    public static IQueryable<EventBeatmap> IncludeExecutor(this IQueryable<EventBeatmap> queryable)
    {
        return queryable.Include(x => x.Executor);
    }
}