using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.API.Services;

public static class UserService
{
    public static List<string> GetUserBadges(User user)
    {
        var badges = new List<string>();

        if (user.Privilege.HasFlag(UserPrivilege.Developer))
            badges.Add("developer");

        if (user.Privilege.HasFlag(UserPrivilege.Admin))
            badges.Add("admin");

        if (user.Privilege.HasFlag(UserPrivilege.Bat))
            badges.Add("bat");

        if (user.Username == Configuration.BotUsername)
            badges.Add("bot");

        if (user.Privilege.HasFlag(UserPrivilege.Supporter))
            badges.Add("supporter");

        if (user.AccountStatus == UserAccountStatus.Restricted)
            badges.Add("restricted");

        return badges;
    }
}