using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Tests.Extensions;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Tests.Services.Mock.Services;

public class MockScoreService(MockService service)
{
    private static readonly string[] BeatmapGradeChars = ["F", "D", "C", "B", "A", "S", "SH", "X", "XH"];

    /// <summary>
    ///     Returns a random score.
    ///     Keep in mind that this score is not normalized, thus it may contain invalid values.
    /// </summary>
    public Score GetRandomScore()
    {
        return new Score
        {
            UserId = service.GetRandomInteger(length: 6),
            BeatmapId = service.GetRandomInteger(length: 6),
            Count300 = service.GetRandomInteger(length: 3),
            Count100 = service.GetRandomInteger(length: 3),
            Count50 = service.GetRandomInteger(length: 3),
            CountGeki = service.GetRandomInteger(length: 3),
            CountKatu = service.GetRandomInteger(length: 3),
            CountMiss = service.GetRandomInteger(length: 3),
            Grade = GetRandomBeatmapGrade(),
            IsScoreable = service.GetRandomBoolean(),
            Mods = GetRandomMods(),
            Accuracy = service.GetRandomInteger(length: 2),
            Perfect = service.GetRandomBoolean(),
            GameMode = GetRandomGameMode(),
            BeatmapStatus = service.Beatmap.GetRandomBeatmapStatus(),
            IsPassed = service.GetRandomBoolean(),
            BeatmapHash = service.GetRandomString(32),
            PerformancePoints = service.GetRandomInteger(length: 3),
            MaxCombo = service.GetRandomInteger(length: 3),
            ScoreHash = service.GetRandomString(32),
            TotalScore = service.GetRandomInteger(length: 6),
            WhenPlayed = service.GetRandomDateTime(),
            ClientTime = service.GetRandomDateTime(),
            OsuVersion = service.GetRandomInteger(length: 6).ToString()
        };
    }


    public PerformanceAttributes GetRandomPerformanceAttributes()
    {
        return new PerformanceAttributes
        {
            PerformancePoints = service.GetRandomInteger(length: 6),
            Difficulty = new DifficultyAttributes
            {
                Aim = service.GetRandomInteger(minInt: 0, maxInt: 10),
                AimDifficultStrainCount = service.GetRandomInteger(minInt: 0, maxInt: 10),
                AR = service.GetRandomInteger(minInt: 0, maxInt: 10),
                Color = service.GetRandomInteger(minInt: 0, maxInt: 10),
                Flashlight = service.GetRandomInteger(minInt: 0, maxInt: 10),
                GreatHitWindow = service.GetRandomInteger(minInt: 0, maxInt: 10),
                HP = service.GetRandomInteger(minInt: 0, maxInt: 10),
                IsConvert = service.GetRandomBoolean(),
                MaxCombo = service.GetRandomInteger(),
                Mode = GetRandomGameMode(),
                MonoStaminaFactor = service.GetRandomInteger(minInt: 0, maxInt: 10),
                NCircles = service.GetRandomInteger(length: 6),
                NDroplets = service.GetRandomInteger(length: 6),
                NFruits = service.GetRandomInteger(length: 6),
                NHoldNotes = service.GetRandomInteger(length: 6),
                NLargeTicks = service.GetRandomInteger(length: 6),
                NObjects = service.GetRandomInteger(length: 6),
                NSliders = service.GetRandomInteger(length: 6),
                NSpinners = service.GetRandomInteger(length: 6),
                NTinyDroplets = service.GetRandomInteger(length: 6),
                OD = service.GetRandomInteger(minInt: 0, maxInt: 10),
                OkHitWindow = service.GetRandomInteger(length: 6),
                Peak = service.GetRandomInteger(length: 6),
                Rhythm = service.GetRandomInteger(length: 6),
                SliderFactor = service.GetRandomInteger(length: 6),
                Speed = service.GetRandomInteger(minInt: 0, maxInt: 10),
                SpeedDifficultStrainCount = service.GetRandomInteger(length: 6),
                SpeedNoteCount = service.GetRandomInteger(length: 6),
                Stamina = service.GetRandomInteger(minInt: 0, maxInt: 10),
                Stars = service.GetRandomInteger(minInt: 0, maxInt: 10)
            },
            State = new ScoreState(),
            EffectiveMissCount = service.GetRandomInteger(length: 6),
            EstimatedUnstableRate = service.GetRandomInteger(length: 6),
            PerformancePointsAccuracy = service.GetRandomInteger(length: 6),
            PerformancePointsAim = service.GetRandomInteger(length: 6),
            PerformancePointsDifficulty = service.GetRandomInteger(length: 6),
            PerformancePointsFlashlight = service.GetRandomInteger(length: 6),
            PerformancePointsSpeed = service.GetRandomInteger(length: 6)
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

        score.LocalProperties = score.LocalProperties.FromScore(score);

        return score;
    }

    public GameMode GetRandomGameMode()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(GameMode));
        return (GameMode)values.GetValue(random.Next(values.Length))!;
    }

    public int GetRandomAccuracy()
    {
        return service.GetRandomInteger(minInt: 0, maxInt: 100);
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