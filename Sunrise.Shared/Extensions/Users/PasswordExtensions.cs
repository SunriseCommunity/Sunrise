using System.Security.Cryptography;
using System.Text;

namespace Sunrise.Shared.Extensions.Users;

public static class PasswordExtensions
{
    public static (bool, string?) IsValidPassword(this string str)
    {
        var isLengthValid = str.Length is >= 8 and <= 32;

        if (!isLengthValid)
        {
            return (false, "Password length must be between 8 and 32 characters");
        }

        if (!str.IsValidStringCharacters())
        {
            return (false, "Password contains invalid characters");
        }

        return (true, null);
    }

    public static string GetPassHash(this string password)
    {
        var data = MD5.HashData(Encoding.UTF8.GetBytes(password));
        var sb = new StringBuilder();

        foreach (var b in data)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}