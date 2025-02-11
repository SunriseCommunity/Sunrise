using System.Text;
using Sunrise.Server.Application;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Extensions;

public static class UsernameExtensions
{
    private static readonly string[] SpecialStrings =
    [
        "guest",
        "_old",
        "filtered"
    ];

    public static string SetUsernameAsOld(this string username)
    {
        return $"{username}_old";
    }

    public static bool IsValidUsername(this string str, bool allowRussian = false)
    {
        if (SpecialStrings.Any(str.ToLower().Contains))
        {
            return false;
        }

        return CharactersFilter.IsValidString(str, allowRussian);
    }

    private static async Task<bool> IsUsernameDisallowed(string str)
    {
        var path = Configuration.BannedUsernamesPath;

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var bannedUsernames = await File.ReadAllLinesAsync(path, Encoding.UTF8);

        for (var i = 0; i < bannedUsernames.Length; i++)
        {
            bannedUsernames[i] = bannedUsernames[i].ToLower().Trim();
        }

        return bannedUsernames.Any(str.Contains);
    }
}