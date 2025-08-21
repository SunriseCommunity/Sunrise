using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.API.Serializable.Request;

public class UpdateBeatmapsCustomStatusRequest
{
    [JsonPropertyName("ids")]
    [Required]
    public required int[] Ids { get; set; }

    [JsonPropertyName("status")]
    [Required]
    public required BeatmapStatusWeb Status { get; set; }
}