using Sunrise.Server.Enums;
using Sunrise.Server.Utils;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Session;

namespace Sunrise.Server.Extensions;

public static class BeatmapSetSearchExtensions
{
    public static string ToSearchResult(this BeatmapSet beatmapSet, Session session)
    {
        var beatmaps = beatmapSet.Beatmaps.GroupBy(x => x.DifficultyRating).OrderBy(x => x.Key).SelectMany(x => x).Aggregate("",
            (current, map) => current + map.ToSearchEntity()).TrimEnd(',');

        var hasVideo = beatmapSet.HasVideo ? "1" : "0";

        var beatmapStatus = BeatmapStatusSearchParser.GetBeatmapSearchStatus(beatmapSet.StatusString);
        var lastUpdatedTime = (beatmapStatus >= BeatmapStatusSearch.Ranked ? beatmapSet.RankedDate : beatmapSet.LastUpdated) + TimeSpan.FromHours(session.Attributes.Timezone);

        return $"{beatmapSet.Id}.osz|{beatmapSet.Artist.Replace('|', 'I')}|{beatmapSet.Title.Replace('|', 'I')}|{beatmapSet.Creator.Replace('|', 'I')}|{(int)beatmapStatus}|10.0|{lastUpdatedTime}|{beatmapSet.Id}|0|{hasVideo}|0|0|0|{beatmaps}";
    }
}