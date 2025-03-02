using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Sunrise.Shared.Extensions;

public static class StringExtensions
{
    public static bool IsValidStringCharacters(this string str)
    {
        var pattern = @"^[a-zA-Z0-9!@#$%^&*()._+]+$";

        return Regex.IsMatch(str, pattern);
    }

    public static string ToHash(this string s)
    {
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(s))).ToLower();
    }
}