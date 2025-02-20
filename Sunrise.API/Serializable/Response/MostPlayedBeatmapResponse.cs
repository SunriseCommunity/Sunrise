using System.Text.Json.Serialization;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Session;

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