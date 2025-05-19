using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Serializable.Performances;

namespace Sunrise.Shared.Extensions.Beatmaps;

public static class BeatmapExtensions
{
    public static void UpdateBeatmapRanking(this Beatmap beatmap, BeatmapStatusWeb beatmapStatus, User? beatmapNominator = null)
    {
        beatmap.StatusString = beatmapStatus.BeatmapStatusWebToString();
        beatmap.Ranked = (int)beatmapStatus;
        beatmap.BeatmapNominatorUser = beatmapNominator;
    }

    public static void UpdateBeatmapWithPerformance(this Beatmap beatmap, Mods mods, PerformanceAttributes performance)
    {
        if (mods.HasFlag(Mods.Easy))
        {
            beatmap.CS /= 2;
        }

        if (mods.HasFlag(Mods.HardRock))
        {
            beatmap.CS *= 1.3;
        }

        if (mods.HasFlag(Mods.DoubleTime))
        {
            beatmap.TotalLength = (int)Math.Floor(beatmap.TotalLength / 1.5);
        }

        if (mods.HasFlag(Mods.HalfTime))
        {
            beatmap.TotalLength = (int)Math.Floor(beatmap.TotalLength * 1.33);
        }

        beatmap.CS = Math.Clamp(beatmap.CS, 0, 10);

        beatmap.AR = performance.Difficulty.AR;
        beatmap.Drain = performance.Difficulty.HP;
        beatmap.Accuracy = performance.Difficulty.OD;

        beatmap.DifficultyRating = performance.Difficulty.Stars;
    }

    public static string GetBeatmapInGameChatString(this Beatmap beatmap, BeatmapSet beatmapSet)
    {
        return $"[https://osu.{Configuration.Domain}/beatmapsets/{beatmap.BeatmapsetId}#/{beatmap.Id} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap.Version}]]";
    }
}