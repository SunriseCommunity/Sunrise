using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Tests.Core.Utils;

public static class MockUtil
{
   public static short GetRandomCountryCode()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(CountryCodes));
        return (short)values.GetValue(random.Next(values.Length))!;
    }
    
    public static string GetRandomPassword()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }
    
    public static string GetRandomIp()
    {
        return $"{new Random().Next(0, 255)}.{new Random().Next(0, 255)}.{new Random().Next(0, 255)}.{new Random().Next(0, 255)}";
    }
    
    public static string GetRandomUsername(int length = 16)
    {
        var baseString = $"{Guid.NewGuid():N}";
        var repeatedString = string.Concat(Enumerable.Repeat(baseString, (length / baseString.Length) + 1));
        
        return repeatedString[..length];
    }

    public static string GetRandomEmail(string? username = null)
    {
        return $"{username ?? GetRandomUsername()}@mail.com";
    }
}