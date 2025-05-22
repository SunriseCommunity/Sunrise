using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Enums;

namespace Sunrise.Shared.Database.Extensions;

public static class EventBeatmapQueryableExtensions
{
    public static IQueryable<EventBeatmap> IncludeExecutor(this IQueryable<EventBeatmap> queryable)
    {
        return queryable
            .Include(x => x.Executor)
            .ThenInclude(u => u.UserFiles.Where(f => f.Type == FileType.Avatar || f.Type == FileType.Banner))
            .AsSingleQuery();
    }
}