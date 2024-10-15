using System.Text.Json.Serialization;
using Sunrise.Server.Objects.Serializable;

namespace Sunrise.Server.API.Serializable.Response;

public class MostPlayedBeatmapResponse(Beatmap beatmap, int playCount, BeatmapSet? beatmapSet = null)
    : BeatmapResponse(beatmap, beatmapSet)
{
    [JsonPropertyName("play_count")] public int PlayCount { get; set; } = playCount;
}