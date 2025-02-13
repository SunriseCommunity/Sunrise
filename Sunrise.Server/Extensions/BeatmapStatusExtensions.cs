using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Extensions;

public static class BeatmapStatusExtensions
{
    public static bool IsRanked(this BeatmapStatus status)
    {
        return status is BeatmapStatus.Ranked or BeatmapStatus.Approved;
    }
}