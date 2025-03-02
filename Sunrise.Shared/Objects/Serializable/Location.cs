using System.Text.Json.Serialization;

namespace Sunrise.Shared.Objects.Serializable;

public class Location
{
    [JsonPropertyName("countryCode")]
    public string Country { get; set; } = "XX";

    [JsonPropertyName("lat")]
    public float Latitude { get; set; } = 0;

    [JsonPropertyName("lon")]
    public float Longitude { get; set; } = 0;

    public string Ip { get; set; } = string.Empty;

    public int TimeOffset { get; set; } = 0;
}