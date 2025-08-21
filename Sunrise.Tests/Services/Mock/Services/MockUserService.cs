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

    public UserGrades SetRandomUserGrades(UserGrades grades)
    {
        grades.CountXH = service.GetRandomInteger(length: 3);
        grades.CountX = service.GetRandomInteger(length: 3);
        grades.CountSH = service.GetRandomInteger(length: 3);
        grades.CountS = service.GetRandomInteger(length: 3);
        grades.CountA = service.GetRandomInteger(length: 3);
        grades.CountB = service.GetRandomInteger(length: 3);
        grades.CountC = service.GetRandomInteger(length: 3);
        grades.CountD = service.GetRandomInteger(length: 3);

        return grades;
    }

    public UserMetadata SetRandomUserMetadata(UserMetadata metadata)
    {
        metadata.Location = service.GetRandomString(32);
        metadata.Occupation = service.GetRandomString(32);
        metadata.Interest = service.GetRandomString(32);

        metadata.Discord = service.GetRandomString(32);
        metadata.Telegram = service.GetRandomString(32);
        metadata.Twitch = service.GetRandomString(32);
        metadata.Twitter = service.GetRandomString(32);
        metadata.Website = service.GetRandomString(200);

        metadata.Playstyle = GetRandomUserPlaystyle();

        return metadata;
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

    public CountryCode GetRandomCountryCode()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(CountryCode))
            .Cast<CountryCode>()
            .Where(v => v != 0)
            .ToArray();
        return (CountryCode)values.GetValue(random.Next(values.Length))!;
    }

    public UserPlaystyle GetRandomUserPlaystyle()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(UserPlaystyle));
        return (UserPlaystyle)values.GetValue(random.Next(values.Length))!;
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