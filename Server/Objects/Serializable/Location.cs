using System.Text.Json.Serialization;

namespace Sunrise.Server.Objects.Serializable;

public class Location
{
    [JsonPropertyName("countryCode")]
    public string Country { get; set; } = "XX";

    [JsonPropertyName("lat")]
    public float Latitude { get; set; } = 0;

    [JsonPropertyName("lon")]
    public float Longitude { get; set; } = 0;

    public int TimeOffset { get; set; } = 0;
}