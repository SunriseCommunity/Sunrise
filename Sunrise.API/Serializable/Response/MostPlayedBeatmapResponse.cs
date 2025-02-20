using System.Text.Json.Serialization;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.API.Serializable.Response;

public class MostPlayedBeatmapResponse : BeatmapResponse
{
    public MostPlayedBeatmapResponse(BaseSession session, Beatmap beatmap, int playCount, BeatmapSet? beatmapSet = null) : base(session, beatmap, beatmapSet)
    {
        PlayCount = playCount;
    }

    [JsonConstructor]
    public MostPlayedBeatmapResponse()
    {
    }

    [JsonPropertyName("play_count")]
    public int PlayCount { get; set; }
}