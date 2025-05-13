using System.Text.Json.Serialization;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Repositories;

namespace Sunrise.API.Serializable.Response;

public class HypedBeatmapSetResponse : BeatmapSetResponse
{
    public HypedBeatmapSetResponse(SessionRepository sessions, BeatmapSet beatmapSet, int hypeCount) : base(sessions, beatmapSet)
    {
        HypeCount = hypeCount;
    }

    [JsonConstructor]
    public HypedBeatmapSetResponse()
    {
    }

    [JsonPropertyName("hypeCount")]
    public int HypeCount { get; set; }
}