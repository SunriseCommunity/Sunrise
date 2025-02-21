using System.Text;
using System.Text.RegularExpressions;
using Sunrise.Shared.Application;

namespace Sunrise.Shared.Extensions.Users;

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

    public static (bool, string?) IsValidUsername(this string str)
    {
        var isLengthValid = str.Length is >= 2 and <= 32;

        if (!isLengthValid)
        {
            return (false, "Username length must be between 2 and 32 characters");
        }

        if (SpecialStrings.Any(str.ToLower().Contains))
        {
            return (false, "Username contains unallowed strings, please remove them and try again.");
        }

        if (IsUsernameDisallowed(str).Result)
        {
            return (false, "Username contains unallowed strings, try to come up with a harmless and original nickname.");
        }

        if (str.StartsWith(" ") || str.EndsWith(" "))
        {
            return (false, "Username cannot start or end with a space");
        }

        if (!str.IsValidUsernameCharacters())
        {
            return (false, "Username contains invalid characters");
        }

        return (true, null);
    }

    public static bool IsValidUsernameCharacters(this string str)
    {
        return Regex.IsMatch(str, @"^[1-9 0-\[\]a-zA-Z_-]+$");
    }

    private static async Task<bool> IsUsernameDisallowed(string str)
    {
        var path = Path.Combine(Configuration.DataPath, Configuration.BannedUsernamesName);

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