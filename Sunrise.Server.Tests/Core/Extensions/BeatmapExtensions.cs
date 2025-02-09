using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Objects.Serializable;

namespace Sunrise.Server.Tests.Core.Extensions;

public static class BeatmapExtensions
{
    public static void EnrichWithScoreData(this Beatmap beatmap, Score score)
    {
        beatmap.Checksum = score.BeatmapHash;
        beatmap.Id = score.BeatmapId;
    }
}