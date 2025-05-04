using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class EditBeatmapsetFavouriteStatusRequest
{
    [JsonPropertyName("favourited")]
    public required bool Favourited { get; set; }
}