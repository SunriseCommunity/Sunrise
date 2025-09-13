using Sunrise.Tests.Services.Mock.Services;

namespace Sunrise.Tests.Services.Mock;

public class MockService
{

    public MockService()
    {
        Score = new MockScoreService(this);
        Beatmap = new MockBeatmapService(this);
        User = new MockUserService(this);
        Redis = new MockRedisService(this);
    }

    public MockScoreService Score { get; }
    public MockBeatmapService Beatmap { get; }
    public MockUserService User { get; }
    public MockRedisService Redis { get; }

    public DateTime GetRandomDateTime()
    {
        var random = new Random();
        var start = new DateTime(2000, 1, 1);
        var range = (DateTime.Today - start).Days;
        return start.AddDays(random.Next(range));
    }

    public bool GetRandomBoolean()
    {
        return new Random().Next(0, 2) == 1;
    }

    public double GetRandomDouble(bool? negative = null)
    {
        var random = new Random();
        return random.NextDouble() * (negative != null ? negative.Value ? -1 : 1 : GetRandomBoolean() ? -1 : 1);
    }

    public string GetRandomString(int length = 16)
    {
        var baseString = $"{Guid.NewGuid():N}";
        var repeatedString = string.Concat(Enumerable.Repeat(baseString, length / baseString.Length + 1));

        return repeatedString[..length];
    }

    public int GetRandomInteger(int? maxInt = null, int? length = null, int minInt = 0)
    {
        if (length.HasValue && maxInt == null)
        {
            maxInt = (int)Math.Pow(10, length.Value);
        }
        else if (maxInt == null)
        {
            maxInt = int.MaxValue;
        }

        return new Random().Next(minInt, maxInt.Value);
    }
}