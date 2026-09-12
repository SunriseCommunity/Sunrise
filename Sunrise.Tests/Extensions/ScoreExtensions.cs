using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Utils;
using Sunrise.Shared.Utils.Calculators;
using Mods = osu.Shared.Mods;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Tests.Extensions;

public static class ScoreExtensions
{
    public static void EnrichWithSessionData(this Score score, Session session, string? storyboardHash = null)
    {
        score.UserId = session.UserId;

        using var scope = ServicesProviderHolder.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SunriseDbContext>();

        var user = dbContext.Users.FirstOrDefault(u => u.Id == session.UserId);
        if (user == null)
            throw new NullReferenceException("User not found");

        score.ScoreHash = score.ComputeOnlineHash(user.Username, session.Attributes.UserHash, storyboardHash);
    }

    public static void EnrichWithUserData(this Score score, User user)
    {
        score.UserId = user.Id;
    }

    public static void EnrichWithBeatmapData(this Score score, Beatmap beatmap)
    {
        score.BeatmapHash = beatmap.Checksum ?? throw new Exception("Beatmap checksum is null");
        score.BeatmapId = beatmap.Id;
        score.BeatmapStatus = beatmap.Status;
    }

    public static void ReconcileModsAndGameMode(this Score score, Beatmap beatmap)
    {
        var vanillaMode = (GameMode)beatmap.ModeInt;
        var requiredModeMod = score.GameMode.GetGamemodeMods();
        var allowedRegularMask = ModsValidationUtil.SupportedMods[vanillaMode.ToVanillaGameMode()]
            .Concat(ModsValidationUtil.IgnoredMods)
            .Aggregate(Mods.None, (mask, mod) => mask | mod);
        var keptMods = score.Mods & allowedRegularMask;

        if (ModsValidationUtil.SupportedModeChangingMods[vanillaMode.ToVanillaGameMode()].Contains(requiredModeMod))
            keptMods |= requiredModeMod;

        foreach (var group in ModsValidationUtil.MutuallyExclusiveGroups)
        {
            var selected = group.Where(mod => keptMods.HasFlag(mod)).ToArray();
            foreach (var extra in selected.Skip(1))
                keptMods &= ~extra;
        }

        score.Mods = keptMods;
        score.GameMode = vanillaMode.EnrichWithMods(score.Mods);
    }

    public static void NormalizeForSubmission(this Score score, Beatmap beatmap)
    {
        score.Count300 = Math.Clamp(score.Count300, 0, ushort.MaxValue);
        score.Count100 = Math.Clamp(score.Count100, 0, ushort.MaxValue);
        score.Count50 = Math.Clamp(score.Count50, 0, ushort.MaxValue);
        score.CountGeki = Math.Clamp(score.CountGeki, 0, ushort.MaxValue);
        score.CountKatu = Math.Clamp(score.CountKatu, 0, ushort.MaxValue);
        score.CountMiss = Math.Clamp(score.CountMiss, 0, ushort.MaxValue);
        score.MaxCombo = Math.Clamp(score.MaxCombo, 0, ushort.MaxValue);
        score.TotalScore = Math.Clamp(score.TotalScore, 0, int.MaxValue);

        var vanillaMode = (GameMode)beatmap.ModeInt;
        var nativeStandard = vanillaMode == GameMode.Standard && beatmap.ModeInt == (int)osu.Shared.GameMode.Standard && !beatmap.Convert;
        if (nativeStandard)
        {
            var objectCount = Math.Max(0L, (long)beatmap.CountCircles + beatmap.CountSliders + beatmap.CountSpinners);
            var maximumRepresentableJudgments = 4L * ushort.MaxValue;

            if (score.IsPassed && objectCount <= maximumRepresentableJudgments)
            {
                PackJudgmentsToTotal(score, objectCount);
            }
            else
            {
                if (score.IsPassed)
                    score.IsPassed = false;

                var submittedJudgments = Math.Min(
                    objectCount,
                    (long)score.Count300 + score.Count100 + score.Count50 + score.CountMiss);
                PackJudgmentsToTotal(score, submittedJudgments);
            }

            if (beatmap.MaxCombo is > 0 and var maxCombo)
                score.MaxCombo = Math.Min(score.MaxCombo, maxCombo);
        }

        if (score.CountMiss > 0)
            score.Perfect = false;

        score.OsuVersion = score.ClientTime.ToString("yyyyMMdd");
        score.Accuracy = PerformanceCalculator.CalculateAccuracy(score);
        score.Grade = ScoreGradeUtil.Calculate(score);
    }

    public static void PrepareForSubmission(this Score score, Beatmap beatmap)
    {
        score.EnrichWithBeatmapData(beatmap);
        score.ReconcileModsAndGameMode(beatmap);
        score.NormalizeForSubmission(beatmap);
    }

    private static void PackJudgmentsToTotal(Score score, long objectCount)
    {
        var remaining = objectCount;
        score.Count300 = (int)Math.Min(remaining, ushort.MaxValue);
        remaining -= score.Count300;
        score.Count100 = (int)Math.Min(remaining, ushort.MaxValue);
        remaining -= score.Count100;
        score.Count50 = (int)Math.Min(remaining, ushort.MaxValue);
        remaining -= score.Count50;
        score.CountMiss = (int)Math.Min(remaining, ushort.MaxValue);
    }

    public static void Normalize(this Score score)
    {
        score.Accuracy = Math.Clamp(score.Accuracy, 0, 100);
        score.Mods = score.GameMode.GetGamemodeMods();
    }

    public static void ToVanillaScore(this Score score)
    {
        score.Mods &= ~score.GameMode.GetGamemodeMods();
        score.GameMode = (GameMode)score.GameMode.ToVanillaGameMode();
    }

    public static void ToBestPerformance(this Score score)
    {
        score.CountKatu = 0;
        score.CountGeki = 0;
        score.CountMiss = 0;
        score.Count50 = 0;
        score.Count100 = 0;
        score.Count300 = int.MaxValue;
        score.MaxCombo = int.MaxValue;
    }
}
