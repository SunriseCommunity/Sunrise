using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.API.Serializable.Request;

public class UpdateBeatmapsCustomStatusRequest
{
    [JsonPropertyName("ids")]
    public required int[] Ids { get; set; }
    
    [JsonPropertyName("status")]
    public required BeatmapStatusWeb Status { get; set; }
}