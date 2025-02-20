using Sunrise.Server.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Types.Enums;
using Sunrise.Shared.Utils;
using ISession = Sunrise.Shared.Types.Interfaces.ISession;

namespace Sunrise.Server.Extensions;

public static class BeatmapSetSearchExtensions
{
    public static string ToSearchResult(this BeatmapSet beatmapSet, ISession session)
    {
        var beatmaps = beatmapSet.Beatmaps.GroupBy(x => x.DifficultyRating).OrderBy(x => x.Key).SelectMany(x => x).Aggregate("",
            (current, map) => current + map.ToSearchEntity()).TrimEnd(',');

        var hasVideo = beatmapSet.HasVideo ? "1" : "0";

        var beatmapStatus = Parsers.GetBeatmapSearchStatus(beatmapSet.StatusString);
        var lastUpdatedTime = (beatmapStatus >= BeatmapStatusSearch.Ranked ? beatmapSet.RankedDate : beatmapSet.LastUpdated) + TimeSpan.FromHours(session.Attributes.Timezone);

        return $"{beatmapSet.Id}.osz|{beatmapSet.Artist.Replace('|', 'I')}|{beatmapSet.Title.Replace('|', 'I')}|{beatmapSet.Creator.Replace('|', 'I')}|{(int)beatmapStatus}|10.0|{lastUpdatedTime}|{beatmapSet.Id}|0|{hasVideo}|0|0|0|{beatmaps}";
    }
}