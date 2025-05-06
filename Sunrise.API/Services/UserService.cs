using Sunrise.API.Enums;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.API.Services;

public static class UserService
{
    public static List<UserBadge> GetUserBadges(User user)
    {
        var badges = new List<UserBadge>();

        if (user.Privilege.HasFlag(UserPrivilege.Developer))
            badges.Add(UserBadge.Developer);

        if (user.Privilege.HasFlag(UserPrivilege.Admin))
            badges.Add(UserBadge.Admin);

        if (user.Privilege.HasFlag(UserPrivilege.Bat))
            badges.Add(UserBadge.Bat);

        if (user.IsUserSunriseBot())
            badges.Add(UserBadge.Bot);

        if (user.Privilege.HasFlag(UserPrivilege.Supporter))
            badges.Add(UserBadge.Supporter);

        return badges;
    }
}