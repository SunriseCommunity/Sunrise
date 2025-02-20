using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Server.Extensions;

public static class BeatmapSearchExtensions
{
    public static string ToSearchEntity(this Beatmap beatmap)
    {
        return $"[{beatmap.DifficultyRating:F2}⭐] {beatmap.Version.Replace('|', 'I')} {{cs: {beatmap.CS} / od: {beatmap.Accuracy} / ar: {beatmap.AR} / hp: {beatmap.Drain}}}@{beatmap.ModeInt},";
    }
}