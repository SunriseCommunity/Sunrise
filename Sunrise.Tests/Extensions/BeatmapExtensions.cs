using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Tests.Extensions;

public static class BeatmapExtensions
{
    public static void EnrichWithScoreData(this Beatmap beatmap, Score score)
    {
        beatmap.Checksum = score.BeatmapHash;
        beatmap.Id = score.BeatmapId;
        beatmap.ModeInt = (int)score.GameMode.ToVanillaGameMode();
        beatmap.Convert = false;
        beatmap.CountCircles = score.Count300 + score.Count100 + score.Count50 + score.CountMiss;
        beatmap.CountSliders = 0;
        beatmap.CountSpinners = 0;
        beatmap.MaxCombo = Math.Max(beatmap.MaxCombo ?? 0, score.MaxCombo);
    }
}
