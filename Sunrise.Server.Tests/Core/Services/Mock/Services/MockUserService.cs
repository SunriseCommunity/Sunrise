using Sunrise.Server.Database.Models;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Tests.Core.Services.Mock.Services;

public class MockUserService(MockService service)
{
    private static readonly string[] BeatmapGradeChars = ["F", "D", "C", "B", "A", "S", "SH", "X", "XH"];
    
    public string GetRandomIp()
    {
        return $"{new Random().Next(0, 255)}.{new Random().Next(0, 255)}.{new Random().Next(0, 255)}.{new Random().Next(0, 255)}";
    }


    public short GetRandomCountryCode()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(CountryCodes));
        return (short)values.GetValue(random.Next(values.Length))!;
    }
    
    public string GetRandomPassword()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }
    
    
    public string GetRandomUsername(int length = 16)
    {
        return service.GetRandomString(length);
    }

    public string GetRandomEmail(string? username = null)
    {
        return $"{username ?? service.GetRandomString()}@mail.com";
    }
}