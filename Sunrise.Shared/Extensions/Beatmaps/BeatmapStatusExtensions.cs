using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Extensions.Beatmaps;

public static class BeatmapStatusExtensions
{
    private static readonly Dictionary<string, BeatmapStatus> _statusMap = new()
    {
        ["loved"] = BeatmapStatus.Loved,
        ["qualified"] = BeatmapStatus.Qualified,
        ["approved"] = BeatmapStatus.Approved,
        ["ranked"] = BeatmapStatus.Ranked,
        ["pending"] = BeatmapStatus.Pending,
        ["graveyard"] = BeatmapStatus.Pending,
        ["wip"] = BeatmapStatus.Pending
    };

    private static readonly Dictionary<BeatmapStatus, string> _statusMapString = new()
    {
        [BeatmapStatus.Loved] = "loved",
        [BeatmapStatus.Qualified] = "qualified",
        [BeatmapStatus.Approved] = "approved",
        [BeatmapStatus.Ranked] = "ranked",
        [BeatmapStatus.Pending] = "pending"
    };

    public static BeatmapStatus StringToBeatmapStatus(this string statusString)
    {
        return _statusMap.GetValueOrDefault(statusString, BeatmapStatus.Pending);
    }

    public static string BeatmapStatusToString(this BeatmapStatus statusString)
    {
        return _statusMapString.GetValueOrDefault(statusString, "pending");
    }

    public static bool IsRanked(this BeatmapStatus status)
    {
        return status is BeatmapStatus.Ranked or BeatmapStatus.Approved;
    }

    public static bool IsScoreable(this BeatmapStatus status)
    {
        return status is BeatmapStatus.Ranked or BeatmapStatus.Approved or BeatmapStatus.Loved;
    }
}