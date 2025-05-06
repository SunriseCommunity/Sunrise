using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Database.Extensions;

public static class UserStatsQueryableExtensions
{
    public static IQueryable<UserStats> FilterValidUserStats(this IQueryable<UserStats> stats)
    {
        return stats
            .Where(us => us.User.AccountStatus != UserAccountStatus.Restricted);
    }

    public static IQueryable<UserStats> IncludeUser(this IQueryable<UserStats> queryable)
    {
        return queryable
            .Include(x => x.User)
            .Include(y => y.User.UserFiles.Where(f => f.Type == FileType.Avatar || f.Type == FileType.Banner))
            .AsSingleQuery();
    }
}