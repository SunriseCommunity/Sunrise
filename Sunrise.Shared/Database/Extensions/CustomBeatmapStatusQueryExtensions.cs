using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Enums;

namespace Sunrise.Shared.Database.Extensions;

public static class CustomBeatmapStatusQueryExtensions
{
    public static IQueryable<CustomBeatmapStatus> IncludeBeatmapNominator(this IQueryable<CustomBeatmapStatus> queryable)
    {
        return queryable
            .Include(x => x.UpdatedByUser)
            .ThenInclude(u => u.UserFiles.Where(f => f.Type == FileType.Avatar || f.Type == FileType.Banner))
            .AsSingleQuery();
    }
}