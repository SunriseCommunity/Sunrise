using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Types.Enums;

namespace Sunrise.API.Services;

public static class UserService
{
    public static List<string> GetUserBadges(User user)
    {
        var badges = new List<string>();

        if (user.Privilege.HasFlag(UserPrivileges.Developer))
            badges.Add("developer");

        if (user.Privilege.HasFlag(UserPrivileges.Admin))
            badges.Add("admin");

        if (user.Privilege.HasFlag(UserPrivileges.Bat))
            badges.Add("bat");

        if (user.Username == Configuration.BotUsername)
            badges.Add("bot");

        if (user.Privilege.HasFlag(UserPrivileges.Supporter))
            badges.Add("supporter");

        if (user.AccountStatus == UserAccountStatus.Restricted)
            badges.Add("restricted");

        return badges;
    }
}