using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class FavouritedResponse
{
    [JsonPropertyName("favourited")]
    public bool Favourited { get; set; }
}