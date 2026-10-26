using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Models.Users;
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
            beatmapSet.UpdateBeatmapRanking(customSetStatus.Status, customSetStatus.UpdatedByUser);
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

        beatmapSet.UpdateBeatmapRanking(status);

        foreach (var beatmap in beatmapSet.Beatmaps)
        {
            beatmap.UpdateBeatmapRanking(status);
        }
    }

    public static void UpdateBeatmapRanking(this BeatmapSet beatmapSet, BeatmapStatusWeb beatmapStatus, User? beatmapNominator = null)
    {
        beatmapSet.StatusString = beatmapStatus.BeatmapStatusWebToString();
        beatmapSet.Ranked = (int)beatmapStatus;
        beatmapSet.BeatmapNominatorUser = beatmapNominator;
    }
}
