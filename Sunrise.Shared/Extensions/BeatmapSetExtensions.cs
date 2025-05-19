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
            beatmapSet.StatusString = customSetStatus.Status.BeatmapStatusWebToString();
            beatmapSet.Ranked = (int)customSetStatus.Status;
            beatmapSet.BeatmapNominatorUser = customSetStatus.UpdatedByUser;
        }

        foreach (var beatmap in beatmapSet.Beatmaps)
        {
            var customStatus = customBeatmapStatuses.FirstOrDefault(s => s.BeatmapHash == beatmap.Checksum);

            if (customStatus != null)
                beatmap.UpdateBeatmapRanking(customStatus.Status, customStatus.UpdatedByUser);
        }
    }

    public static void IgnoreBeatmapRanking(this BeatmapSet beatmapSet)
    {
        var status = BeatmapStatusWeb.Ranked;

        beatmapSet.StatusString = status.BeatmapStatusWebToString();
        beatmapSet.Ranked = (int)status;

        foreach (var beatmap in beatmapSet.Beatmaps)
        {
            beatmap.UpdateBeatmapRanking(status);
        }
    }
}