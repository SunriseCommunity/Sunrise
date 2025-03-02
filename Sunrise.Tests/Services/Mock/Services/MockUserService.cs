using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;

namespace Sunrise.Tests.Services.Mock.Services;

public class MockUserService(MockService service)
{
    private static readonly string[] BeatmapGradeChars = ["F", "D", "C", "B", "A", "S", "SH", "X", "XH"];

    public string GetRandomIp()
    {
        return $"{new Random().Next(0, 255)}.{new Random().Next(0, 255)}.{new Random().Next(0, 255)}.{new Random().Next(0, 255)}";
    }

    public UserStats GetRandomUserStats()
    {
        return new UserStats
        {
            UserId = service.GetRandomInteger(length: 6),
            GameMode = service.Score.GetRandomGameMode(),
            TotalScore = service.GetRandomInteger(length: 6),
            TotalHits = service.GetRandomInteger(length: 3),
            MaxCombo = service.GetRandomInteger(length: 3),
            PlayTime = service.GetRandomInteger(length: 3),
            PlayCount = service.GetRandomInteger(length: 3),
            RankedScore = service.GetRandomInteger(length: 6),
            PerformancePoints = service.GetRandomInteger(length: 3),
            Accuracy = service.GetRandomInteger(length: 2)
        };
    }

    public User GetRandomUser()
    {
        var username = GetRandomUsername();
        return GetRandomUser(username);
    }

    public User GetRandomUser(string username)
    {
        return new User
        {
            Username = username,
            Email = GetRandomEmail(username),
            Passhash = GetRandomPassword().GetPassHash(),
            Country = GetRandomCountryCode()
        };
    }

    public StatsSnapshot GetRandomStatsSnapshot()
    {
        return new StatsSnapshot
        {
            Rank = service.GetRandomInteger(length: 6),
            CountryRank = service.GetRandomInteger(length: 6),
            PerformancePoints = service.GetRandomInteger(length: 3)
        };
    }

    public short GetRandomCountryCode()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(CountryCode));
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