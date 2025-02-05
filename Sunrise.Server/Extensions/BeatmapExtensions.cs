using Sunrise.Server.Application;
using Sunrise.Server.Objects.Serializable;

namespace Sunrise.Server.Extensions;

public static class BeatmapExtensions
{
    public static string ToSearchEntity(this Beatmap beatmap)
    {
        return $"[{beatmap.DifficultyRating:F2}⭐] {beatmap.Version.Replace('|', 'I')} {{cs: {beatmap.CS} / od: {beatmap.Accuracy} / ar: {beatmap.AR} / hp: {beatmap.Drain}}}@{beatmap.ModeInt},";
    }

    public static void UpdateBeatmapRanking(this Beatmap beatmap)
    {
        if (!Configuration.IgnoreBeatmapRanking) return;

        beatmap.StatusString = "ranked";
        beatmap.IsScoreable = true;
        beatmap.Ranked = 1;
    }
}