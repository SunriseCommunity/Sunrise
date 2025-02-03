using System.Text.RegularExpressions;

namespace Sunrise.Server.Utils;

public static class CharactersFilter
{
    public static bool IsValidString(string str, bool allowRussian = false)
    {
        var pattern = allowRussian ? @"^[a-zA-Z0-9а-яА-Я!@#$%^&*()._+]+$" : @"^[a-zA-Z0-9!@#$%^&*()._+]+$";

        return Regex.IsMatch(str, pattern);
    }

    public static bool IsValidEmail(this string str)
    {
        return Regex.IsMatch(str, @"^.+@.+\.[a-zA-Z]{2,256}$");
    }
}