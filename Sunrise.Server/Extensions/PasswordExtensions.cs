using Sunrise.Server.Utils;

namespace Sunrise.Server.Extensions;

public static class PasswordExtensions
{
    public static (bool, string?) IsValidPassword(this string str)
    {
        var isLengthValid = str.Length is >= 8 and <= 32;

        if (!isLengthValid)
        {
            return (false, "Password length must be between 8 and 32 characters");
        }

        if (!CharactersFilter.IsValidStringCharacters(str))
        {
            return (false, "Password contains invalid characters");
        }

        return (true, null);
    }
}