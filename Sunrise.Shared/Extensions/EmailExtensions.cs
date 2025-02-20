using System.Text.RegularExpressions;

namespace Sunrise.Shared.Extensions;

public static class EmailExtensions
{
    public static bool IsValidEmailCharacters(this string str)
    {
        return Regex.IsMatch(str, @"^.+@.+\.[a-zA-Z]{2,256}$");
    }
}