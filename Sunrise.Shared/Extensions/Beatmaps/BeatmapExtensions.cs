using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Shared.Extensions.Beatmaps;

public static class BeatmapExtensions
{
    public static void UpdateBeatmapRanking(this Beatmap beatmap)
    {
        if (!Configuration.IgnoreBeatmapRanking) return;

        beatmap.StatusString = "ranked";
        beatmap.IsScoreable = true;
        beatmap.Ranked = 1;
    }
}