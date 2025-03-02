using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Shared.Extensions.Beatmaps;

public static class BeatmapExtensions
{
    public static void UpdateBeatmapRanking(this Beatmap beatmap)
    {
        if (!Configuration.IgnoreBeatmapRanking) return;

        beatmap.StatusString = "ranked";
        beatmap.IsScoreable = true;
        beatmap.Ranked = 1;
    }
    
    public static string GetBeatmapInGameChatString(this Beatmap beatmap, BeatmapSet beatmapSet)
    {
        return  $"[{beatmap.Url.Replace("osu.ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap.Version}]]";
    }
    
   
}