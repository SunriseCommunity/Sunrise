using System.Text.Json.Serialization;
using Sunrise.Shared.Application;

namespace Sunrise.API.Serializable.Response;

public class BeatmapSetHypeCountResponse
{
    [JsonPropertyName("current_hypes")]
    public int CurrentHypes { get; set; }

    [JsonPropertyName("required_hypes")]
    public int RequiredHypes { get; set; } = Configuration.HypesToStartHypeTrain;
}