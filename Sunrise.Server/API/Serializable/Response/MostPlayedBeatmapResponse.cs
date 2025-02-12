using System.Text.Json.Serialization;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;

namespace Sunrise.Server.API.Serializable.Response;

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