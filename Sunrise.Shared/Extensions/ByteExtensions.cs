using System.Security.Cryptography;

namespace Sunrise.Shared.Extensions;

public static class ByteExtensions
{
    public static string GetHashSHA1(this byte[] data)
    {
        using var sha1 = new SHA1CryptoServiceProvider();
        return string.Concat(sha1.ComputeHash(data).Select(x => x.ToString("X2")));
    }
}
