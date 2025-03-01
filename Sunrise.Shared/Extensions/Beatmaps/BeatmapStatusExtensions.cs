using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Extensions.Beatmaps;

public static class BeatmapStatusExtensions
{
    public static bool IsRanked(this BeatmapStatus status)
    {
        return status is BeatmapStatus.Ranked or BeatmapStatus.Approved;
    }
    
    public static bool IsScoreable(this BeatmapStatus status)
    {
        return status is BeatmapStatus.Ranked or BeatmapStatus.Approved or BeatmapStatus.Loved;
    }
}