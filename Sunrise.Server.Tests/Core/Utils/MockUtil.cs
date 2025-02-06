using osu.Shared;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Types.Enums;
using GameMode = Sunrise.Server.Types.Enums.GameMode;
using SubmissionStatus = Sunrise.Server.Types.Enums.SubmissionStatus;

namespace Sunrise.Server.Tests.Core.Utils;

public static class MockUtil
{
    private static readonly string[] BeatmapGradeChars = ["F", "D", "C", "B", "A", "S", "SH", "X", "XH"];

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
    
    public static int GetRandomInteger(int? maxInt = null, int? length = null)
    {
        if (length.HasValue && maxInt == null)
        {
            maxInt = (int)Math.Pow(10, length.Value);
        }
        else if (maxInt == null)
        {
            maxInt = int.MaxValue;
        }
        
        return new Random().Next(0, maxInt.Value);
    }

    public static Score GetRandomScore()
    {
        return new Score()
        {
            UserId = GetRandomInteger(length:6),
            BeatmapId = GetRandomInteger(length:6),
            Count300 = GetRandomInteger(length:3),
            Count100 = GetRandomInteger(length:3),
            Count50 = GetRandomInteger(length:3),
            CountGeki = GetRandomInteger(length:3),
            CountKatu = GetRandomInteger(length:3),
            CountMiss = GetRandomInteger(length:3),
            Grade = GetRandomBeatmapGrade(),
            IsScoreable = GetRandomBoolean(),
            Mods = GetRandomMods(),
            Accuracy = GetRandomInteger(length:2),
            Perfect = GetRandomBoolean(),
            GameMode = GetRandomGameMode(),
            BeatmapStatus = GetRandomBeatmapStatus(),
            IsPassed = GetRandomBoolean(),
            BeatmapHash = GetRandomString(32),
            PerformancePoints = GetRandomInteger(length:3),
            MaxCombo = GetRandomInteger(length:3),
            ScoreHash = GetRandomString(32),
            TotalScore = GetRandomInteger(length:6),
            WhenPlayed = GetRandomDateTime(),
            ClientTime = GetRandomDateTime(),
            ReplayFileId = GetRandomInteger(length:6),
            OsuVersion = GetRandomInteger(length:6).ToString()
        };
    }
    
    public static Score GetBestScoreableRandomScore()
    {
        var score = GetRandomScore();
        score.SubmissionStatus = SubmissionStatus.Best;
        score.IsScoreable = true;
        score.IsPassed = true;
        
        return score;
    }

    public static DateTime GetRandomDateTime()
    {
        var random = new Random();
        var start = new DateTime(2000, 1, 1);
        var range = (DateTime.Today - start).Days;
        return start.AddDays(random.Next(range));
    }
    
    public static bool GetRandomBoolean()
    {
        return new Random().Next(0, 2) == 1;
    }
    
    public static GameMode GetRandomGameMode()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(GameMode));
        return (GameMode)values.GetValue(random.Next(values.Length))!;
    }
    
    public static BeatmapStatus GetRandomBeatmapStatus()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(BeatmapStatus));
        return (BeatmapStatus)values.GetValue(random.Next(values.Length))!;
    }
    
    public static string GetRandomBeatmapGrade()
    {
        return BeatmapGradeChars[new Random().Next(0, BeatmapGradeChars.Length)];
    }
    
    public static string GetRandomIp()
    {
        return $"{new Random().Next(0, 255)}.{new Random().Next(0, 255)}.{new Random().Next(0, 255)}.{new Random().Next(0, 255)}";
    }

    public static Mods GetRandomMods()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(Mods));
        return (Mods)values.GetValue(random.Next(values.Length))!;
    }
    
    public static string GetRandomString(int length = 16)
    {
        var baseString = $"{Guid.NewGuid():N}";
        var repeatedString = string.Concat(Enumerable.Repeat(baseString, (length / baseString.Length) + 1));
        
        return repeatedString[..length];
    }
    
    public static string GetRandomUsername(int length = 16)
    {
        return GetRandomString(length);
    }

    public static string GetRandomEmail(string? username = null)
    {
        return $"{username ??GetRandomString()}@mail.com";
    }
}