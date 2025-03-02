using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Database.Extensions;

public static class UserStatsQueryableExtensions
{
    public static IQueryable<UserStats> FilterValidUserStats(this IQueryable<UserStats> stats)
    {
        return stats
            .Where(us => us.User.AccountStatus != UserAccountStatus.Restricted);
    }
}