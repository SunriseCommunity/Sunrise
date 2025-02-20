using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Server.Tests.Core.Extensions;

public static class BeatmapExtensions
{
    public static void EnrichWithScoreData(this Beatmap beatmap, Score score)
    {
        beatmap.Checksum = score.BeatmapHash;
        beatmap.Id = score.BeatmapId;
        beatmap.IsScoreable = score.IsScoreable;
    }
}