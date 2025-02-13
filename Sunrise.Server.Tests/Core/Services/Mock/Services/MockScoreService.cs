using osu.Shared;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Tests.Core.Extensions;
using Sunrise.Server.Types.Enums;
using GameMode = Sunrise.Server.Types.Enums.GameMode;
using SubmissionStatus = Sunrise.Server.Types.Enums.SubmissionStatus;

namespace Sunrise.Server.Tests.Core.Services.Mock.Services;

public class MockScoreService(MockService service)
{
    private static readonly string[] BeatmapGradeChars = ["F", "D", "C", "B", "A", "S", "SH", "X", "XH"];
    
    /// <summary>
    /// Returns a random score.
    /// Keep in mind that this score is not normalized, thus it may contain invalid values.
    /// </summary>
    public Score GetRandomScore()
    {
        return new Score()
        {
            UserId = service.GetRandomInteger(length:6),
            BeatmapId = service.GetRandomInteger(length:6),
            Count300 = service.GetRandomInteger(length:3),
            Count100 = service.GetRandomInteger(length:3),
            Count50 = service.GetRandomInteger(length:3),
            CountGeki = service.GetRandomInteger(length:3),
            CountKatu = service.GetRandomInteger(length:3),
            CountMiss = service.GetRandomInteger(length:3),
            Grade = GetRandomBeatmapGrade(),
            IsScoreable = service.GetRandomBoolean(),
            Mods = GetRandomMods(),
            Accuracy = service.GetRandomInteger(length:2),
            Perfect = service.GetRandomBoolean(),
            GameMode = GetRandomGameMode(),
            BeatmapStatus = service.Beatmap.GetRandomBeatmapStatus(),
            IsPassed = service.GetRandomBoolean(),
            BeatmapHash = service.GetRandomString(32),
            PerformancePoints = service.GetRandomInteger(length:3),
            MaxCombo = service.GetRandomInteger(length:3),
            ScoreHash = service.GetRandomString(32),
            TotalScore = service.GetRandomInteger(length:6),
            WhenPlayed = service.GetRandomDateTime(),
            ClientTime = service.GetRandomDateTime(),
            ReplayFileId = service.GetRandomInteger(length:6),
            OsuVersion = service.GetRandomInteger(length:6).ToString()
        };
    }
    
    public Score GetBestScoreableRandomScore()
    {
        var score = GetRandomScore();
        score.BeatmapStatus = BeatmapStatus.Ranked;
        score.SubmissionStatus = SubmissionStatus.Best;
        score.IsScoreable = true;
        score.IsPassed = true;
        
        score.Normalize();
        
        return score;
    }
    
    public GameMode GetRandomGameMode()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(GameMode));
        return (GameMode)values.GetValue(random.Next(values.Length))!;
    }
    
    public string GetRandomBeatmapGrade()
    {
        return BeatmapGradeChars[new Random().Next(0, BeatmapGradeChars.Length)];
    }
  
    public Mods GetRandomMods()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(Mods));
        return (Mods)values.GetValue(random.Next(values.Length))!;
    }

}