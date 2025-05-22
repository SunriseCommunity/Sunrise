using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Server.Utils;

public class BeatmapStatusSearchParser
{
    public static BeatmapStatusWeb GetBeatmapSearchStatus(string status)
    {
        var enumValue = Enum.TryParse(status, true, out BeatmapStatusWeb result) ? result : BeatmapStatusWeb.Pending;
        return enumValue;
    }

    public static BeatmapStatusWeb WebStatusToSearchStatus(int ranked)
    {
        return ranked switch
        {
            0 or 7 => BeatmapStatusWeb.Ranked,
            8 => BeatmapStatusWeb.Loved,
            3 => BeatmapStatusWeb.Qualified,
            2 => BeatmapStatusWeb.Pending,
            5 => BeatmapStatusWeb.Graveyard,
            _ => BeatmapStatusWeb.Unknown
        };
    }
}