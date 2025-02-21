using Sunrise.Shared.Application;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Shared.Extensions;

public static class BeatmapSetExtensions
{
    public static void UpdateBeatmapRanking(this BeatmapSet beatmapSet)
    {
        if (!Configuration.IgnoreBeatmapRanking) return;

        beatmapSet.StatusString = "ranked";
        beatmapSet.IsScoreable = true;
        beatmapSet.Ranked = 1;

        foreach (var beatmap in beatmapSet.Beatmaps)
        {
            beatmap.UpdateBeatmapRanking();
        }
    }
}