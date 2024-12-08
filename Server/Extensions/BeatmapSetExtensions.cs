using Sunrise.Server.Application;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Extensions;

public static class BeatmapSetExtensions
{
    public static string ToSearchResult(this BeatmapSet beatmapSet, Session session)
    {
        var beatmaps = beatmapSet.Beatmaps.GroupBy(x => x.DifficultyRating).OrderBy(x => x.Key).SelectMany(x => x).Aggregate("",
            (current, map) => current + map.ToSearchEntity()).TrimEnd(',');

        var hasVideo = beatmapSet.HasVideo ? "1" : "0";

        var beatmapStatus = Parsers.GetBeatmapSearchStatus(beatmapSet.StatusString);
        var lastUpdatedTime = (beatmapStatus >= BeatmapStatusSearch.Ranked ? beatmapSet.RankedDate : beatmapSet.LastUpdated) + TimeSpan.FromHours(session.Attributes.Timezone);

        return $"{beatmapSet.Id}.osz|{beatmapSet.Artist.Replace('|', 'I')}|{beatmapSet.Title.Replace('|', 'I')}|{beatmapSet.Creator.Replace('|', 'I')}|{(int)beatmapStatus}|10.0|{lastUpdatedTime}|{beatmapSet.Id}|0|{hasVideo}|0|0|0|{beatmaps}";
    }

    public static void UpdateBeatmapRanking(this BeatmapSet beatmapSet)
    {
        if (!Configuration.IgnoreBeatmapRanking) return;

        beatmapSet.StatusString = "ranked";
        beatmapSet.IsScoreable = true;
        beatmapSet.Ranked = 1;

        foreach (var beatmap in beatmapSet.Beatmaps)
        {
            beatmap.UpdateBeatmapRanking();
        }
    }
}