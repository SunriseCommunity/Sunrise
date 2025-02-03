using System.Text;
using System.Text.RegularExpressions;
using Sunrise.Server.Application;

namespace Sunrise.Server.Utils;

public static class CharactersFilter
{
    private static readonly string[] DisallowedStrings =
    [
        "_old",
        "filtered"
    ];

    public static bool IsValidString(string str, bool allowRussian = false)
    {
        var pattern = allowRussian ? @"^[a-zA-Z0-9а-яА-Я!@#$%^&*()._+]+$" : @"^[a-zA-Z0-9!@#$%^&*()._+]+$";

        return Regex.IsMatch(str, pattern);
    }

    public static bool IsValidUsername(string str, bool allowRussian = false)
    {
        if (DisallowedStrings.Any(str.ToLower().Contains))
        {
            return false;
        }

        return IsValidString(str, allowRussian) && !IsUsernameDisallowed(str.ToLower()).Result;
    }

    public static bool IsValidEmail(this string str)
    {
        return Regex.IsMatch(str, @"^.+@.+\.[a-zA-Z]{2,256}$");
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