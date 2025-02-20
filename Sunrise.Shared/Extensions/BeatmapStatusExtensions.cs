using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Extensions;

public static class BeatmapStatusExtensions
{
    public static bool IsRanked(this BeatmapStatus status)
    {
        return status is BeatmapStatus.Ranked or BeatmapStatus.Approved;
    }
}