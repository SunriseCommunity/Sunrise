using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Shared.Extensions;

public static class BeatmapSetExtensions
{
    public static void UpdateBeatmapRanking(this BeatmapSet beatmapSet, List<CustomBeatmapStatus> customBeatmapStatuses)
    {
        if (Configuration.IgnoreBeatmapRanking)
        {
            beatmapSet.IgnoreBeatmapRanking();
            return;
        }

        var customSetStatus = customBeatmapStatuses.OrderByDescending(s => s.Status).FirstOrDefault();

        if (customSetStatus != null)
        {
            beatmapSet.StatusString = customSetStatus.Status.BeatmapStatusToString();
            beatmapSet.Ranked = customSetStatus.Status.IsRanked() ? 1 : 0; // TODO: Should use https://osu.ppy.sh/docs/#beatmapset-rank-status
        }

        foreach (var beatmap in beatmapSet.Beatmaps)
        {
            var customStatus = customBeatmapStatuses.FirstOrDefault(s => s.BeatmapHash == beatmap.Checksum);

            if (customStatus != null)
                beatmap.UpdateBeatmapRanking(customStatus.Status);
        }
    }

    public static void IgnoreBeatmapRanking(this BeatmapSet beatmapSet)
    {
        beatmapSet.StatusString = "ranked";
        beatmapSet.Ranked = 1;

        foreach (var beatmap in beatmapSet.Beatmaps)
        {
            beatmap.UpdateBeatmapRanking(BeatmapStatus.Ranked);
        }
    }
}