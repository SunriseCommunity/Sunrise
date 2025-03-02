using Sunrise.Server.Enums;

namespace Sunrise.Server.Utils;

public class BeatmapStatusSearchParser
{
    public static BeatmapStatusSearch GetBeatmapSearchStatus(string status)
    {
        var enumValue = Enum.TryParse(status, true, out BeatmapStatusSearch result) ? result : BeatmapStatusSearch.Pending;
        return enumValue;
    }

    public static BeatmapStatusSearch WebStatusToSearchStatus(int ranked)
    {
        return ranked switch
        {
            0 or 7 => BeatmapStatusSearch.Ranked,
            8 => BeatmapStatusSearch.Loved,
            3 => BeatmapStatusSearch.Qualified,
            2 => BeatmapStatusSearch.Pending,
            5 => BeatmapStatusSearch.Graveyard,
            _ => BeatmapStatusSearch.Any
        };
    }
}