using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Tests.Extensions;

public static class BeatmapExtensions
{
    public static void EnrichWithScoreData(this Beatmap beatmap, Score score)
    {
        beatmap.Checksum = score.BeatmapHash;
        beatmap.Id = score.BeatmapId;
    }
}