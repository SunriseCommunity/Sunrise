using System.Text.Json.Serialization;

namespace Sunrise.GameClient.Objects.Serializable;

public class Location
{
    [JsonPropertyName("country")]
    public string Country { get; set; } = "XX";

    [JsonPropertyName("loc")]
    public string Loc { get; set; } = "0,0";

    public int TimeOffset { get; set; } = 0;
}


