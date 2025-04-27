using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Database.Extensions;

public static class UserQueryableExtensions
{
    public static IQueryable<User> FilterValidUsers(this IQueryable<User> users)
    {
        return users
            .Where(u => u.AccountStatus != UserAccountStatus.Restricted);
    }

    public static IQueryable<User> FilterActiveUsers(this IQueryable<User> users)
    {
        return users.Where(u => u.AccountStatus == UserAccountStatus.Active);
    }

    public static IQueryable<User> IncludeUserThumbnails(this IQueryable<User> queryable)
    {
        return queryable.Include(u => u.UserFiles.Where(f => f.Type == FileType.Avatar || f.Type == FileType.Banner));
    }
}