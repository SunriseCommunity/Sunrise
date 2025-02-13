using System.Text.RegularExpressions;

namespace Sunrise.Server.Utils;

public static class CharactersFilter
{
    public static bool IsValidStringCharacters(string str)
    {
        var pattern = @"^[a-zA-Z0-9!@#$%^&*()._+]+$";

        return Regex.IsMatch(str, pattern);
    }

    public static bool IsValidUsernameCharacters(string str)
    {
        return Regex.IsMatch(str, @"^[1-9 0-\[\]a-zA-Z_-]+$");
    }

    public static bool IsValidEmailCharacters(this string str)
    {
        return Regex.IsMatch(str, @"^.+@.+\.[a-zA-Z]{2,256}$");
    }
}