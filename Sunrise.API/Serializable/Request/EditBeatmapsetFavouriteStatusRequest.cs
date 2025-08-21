using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class EditBeatmapsetFavouriteStatusRequest
{
    [JsonPropertyName("favourited")]
    [Required]
    public required bool Favourited { get; set; }
}