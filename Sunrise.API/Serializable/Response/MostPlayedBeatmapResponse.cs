using System.Text.Json.Serialization;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Repositories;

namespace Sunrise.API.Serializable.Response;

public class MostPlayedBeatmapResponse : BeatmapResponse
{
    public MostPlayedBeatmapResponse(SessionRepository sessions, Beatmap beatmap, int playCount, BeatmapSet? beatmapSet = null) : base(sessions, beatmap, beatmapSet)
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