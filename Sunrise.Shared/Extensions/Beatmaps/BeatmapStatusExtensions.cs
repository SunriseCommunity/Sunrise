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

    private static readonly Dictionary<string, BeatmapStatusWeb> _statusSearchMap = new()
    {
        ["loved"] = BeatmapStatusWeb.Loved,
        ["qualified"] = BeatmapStatusWeb.Qualified,
        ["approved"] = BeatmapStatusWeb.Approved,
        ["ranked"] = BeatmapStatusWeb.Ranked,
        ["pending"] = BeatmapStatusWeb.Pending,
        ["graveyard"] = BeatmapStatusWeb.Graveyard,
        ["wip"] = BeatmapStatusWeb.Wip
    };

    private static readonly Dictionary<BeatmapStatus, string> _statusMapString = new()
    {
        [BeatmapStatus.Loved] = "loved",
        [BeatmapStatus.Qualified] = "qualified",
        [BeatmapStatus.Approved] = "approved",
        [BeatmapStatus.Ranked] = "ranked",
        [BeatmapStatus.Pending] = "pending"
    };

    private static readonly Dictionary<BeatmapStatusWeb, string> _statusWebMapString = new()
    {
        [BeatmapStatusWeb.Loved] = "loved",
        [BeatmapStatusWeb.Qualified] = "qualified",
        [BeatmapStatusWeb.Approved] = "approved",
        [BeatmapStatusWeb.Ranked] = "ranked",
        [BeatmapStatusWeb.Pending] = "pending",
        [BeatmapStatusWeb.Graveyard] = "graveyard",
        [BeatmapStatusWeb.Wip] = "wip"

    };

    public static BeatmapStatus StringToBeatmapStatus(this string statusString)
    {
        return _statusMap.GetValueOrDefault(statusString, BeatmapStatus.Pending);
    }

    public static BeatmapStatusWeb StringToBeatmapStatusSearch(this string statusString)
    {
        return _statusSearchMap.GetValueOrDefault(statusString, BeatmapStatusWeb.Pending);
    }

    public static string BeatmapStatusWebToString(this BeatmapStatusWeb statusString)
    {
        return _statusWebMapString.GetValueOrDefault(statusString, "pending");
    }

    public static bool IsRanked(this BeatmapStatus status)
    {
        return status is BeatmapStatus.Ranked or BeatmapStatus.Approved;
    }

    public static bool IsRanked(this BeatmapStatusWeb status)
    {
        return status is BeatmapStatusWeb.Ranked or BeatmapStatusWeb.Approved;
    }

    public static bool IsScoreable(this BeatmapStatus status)
    {
        return status is BeatmapStatus.Ranked or BeatmapStatus.Approved or BeatmapStatus.Loved or BeatmapStatus.Qualified;
    }

    public static bool IsScoreable(this BeatmapStatusWeb status)
    {
        return status is BeatmapStatusWeb.Ranked or BeatmapStatusWeb.Approved or BeatmapStatusWeb.Loved or BeatmapStatusWeb.Qualified;
    }
}