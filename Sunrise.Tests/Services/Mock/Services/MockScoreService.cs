using Sunrise.Shared.Database.Models.Beatmap;
using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Utils;
using Sunrise.Shared.Utils.Calculators;
using Sunrise.Tests.Extensions;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Tests.Services.Mock.Services;

public class MockScoreService(MockService service)
{
    /// <summary>
    ///     Returns a random score.
    ///     Keep in mind that this score is not normalized, thus it may contain invalid values.
    /// </summary>
    public Score GetRandomScore(GameMode gameMode = GameMode.Standard)
    {
        var score = new Score
        {
            UserId = service.GetRandomInteger(length: 6),
            BeatmapId = service.GetRandomInteger(length: 6),
            Count300 = service.GetRandomInteger(length: 3),
            Count100 = service.GetRandomInteger(length: 3),
            Count50 = service.GetRandomInteger(length: 3),
            CountGeki = service.GetRandomInteger(length: 3),
            CountKatu = service.GetRandomInteger(length: 3),
            CountMiss = service.GetRandomInteger(length: 3),
            Grade = GetRandomScoreGrade(),
            IsScoreable = service.GetRandomBoolean(),
            Accuracy = service.GetRandomInteger(minInt: 0, maxInt: 100),
            Perfect = service.GetRandomBoolean(),
            GameMode = gameMode,
            BeatmapHashStatus = new BeatmapHashStatus { BeatmapHash = string.Empty, Status = service.Beatmap.GetRandomBeatmapStatus() },
            IsPassed = service.GetRandomBoolean(),
            BeatmapHash = service.GetRandomString(32),
            PerformancePoints = service.GetRandomInteger(length: 3),
            MaxCombo = service.GetRandomInteger(length: 3),
            ScoreHash = service.GetRandomString(32),
            TotalScore = service.GetRandomInteger(length: 6),
            WhenPlayed = service.GetRandomDateTime(),
            ClientTime = service.GetRandomDateTime(),
            OsuVersion = service.GetRandomInteger(length: 8).ToString()
        };

        score.Mods = GetRandomMods(score.GameMode);

        return score;
    }

    /// <summary>
    ///     Returns a score built from random inputs but with dependent values reconciled against the supplied beatmap,
    ///     so it satisfies the production admission rules. Use this instead of <see cref="GetRandomScore" /> when a test
    ///     needs a valid score rather than an arbitrary one.
    /// </summary>
    public Score GetValidScore(Beatmap beatmap)
    {
        var score = GetRandomScore();
        score.PrepareForSubmission(beatmap);

        return score;
    }

    public ScoreGrade GetRandomScoreGrade()
    {
        var values = Enum.GetValues<ScoreGrade>();
        return values[Random.Shared.Next(values.Length)];
    }

    public SubmittedScore GetRandomSubmittedScore(Score score)
    {
        var submittedScore = new SubmittedScore
        {
            PlayerUsername = service.GetRandomString(),
            Count300 = score.Count300,
            Count100 = score.Count100,
            Count50 = score.Count50,
            CountGeki = score.CountGeki,
            CountKatu = score.CountKatu,
            CountMiss = score.CountMiss,
            Grade = score.Grade,
            Accuracy = score.Accuracy,
            Perfect = score.Perfect,
            GameMode = score.GameMode,
            Mods = score.Mods,
            IsPassed = score.IsPassed,
            BeatmapHash = score.BeatmapHash,
            MaxCombo = score.MaxCombo,
            ScoreHash = score.ScoreHash,
            TotalScore = score.TotalScore,
            WhenPlayed = score.WhenPlayed,
            ClientTime = score.ClientTime,
            OsuVersion = score.OsuVersion
        };

        return submittedScore;
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
                Mode = GameMode.Standard,
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
        score.BeatmapHashStatus!.Status = BeatmapStatus.Ranked;
        score.SubmissionStatus = SubmissionStatus.Best;
        score.IsScoreable = true;
        score.IsPassed = true;

        score.Normalize();

        score.OsuVersion = score.ClientTime.ToString("yyyyMMdd");
        if (score.CountMiss > 0)
            score.Perfect = false;

        score.Accuracy = PerformanceCalculator.CalculateAccuracy(score);
        score.Grade = ScoreGradeUtil.Calculate(score);


        return score;
    }

    public GameMode GetRandomGameMode()
    {
        var values = Enum.GetValues(typeof(GameMode));
        return (GameMode)values.GetValue(Random.Shared.Next(values.Length))!;
    }

    public int GetRandomAccuracy()
    {
        return service.GetRandomInteger(minInt: 0, maxInt: 100);
    }

    public Mods GetRandomMods(GameMode gameMode)
    {
        var random = Random.Shared;
        var vanillaMode = gameMode.ToVanillaGameMode();
        var selectedMods = gameMode.GetGamemodeMods();
        var supportedMods = ModsValidationUtil.SupportedMods[vanillaMode];
        var modsInConflictGroups = ModsValidationUtil.MutuallyExclusiveGroups
            .SelectMany(group => group)
            .ToHashSet();

        foreach (var group in ModsValidationUtil.MutuallyExclusiveGroups)
        {
            var compatibleChoices = group
                .Where(supportedMods.Contains)
                .ToArray();

            if (compatibleChoices.Length > 0 && random.Next(compatibleChoices.Length + 1) > 0)
                selectedMods |= compatibleChoices[random.Next(compatibleChoices.Length)];
        }

        foreach (var mod in supportedMods.Where(mod => !modsInConflictGroups.Contains(mod)))
        {
            if (random.Next(2) == 1)
                selectedMods |= mod;
        }

        return selectedMods;
    }
}
