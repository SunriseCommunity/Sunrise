using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class SolarSystemVersionResponse(bool isRunningUnderSolarSystem, string? solarSystemVersion)
{
    [JsonPropertyName("is_running_under_solar_system")]
    public bool IsRunningUnderSolarSystem { get; set; } = isRunningUnderSolarSystem;

    [JsonPropertyName("solar_system_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SolarSystemVersion { get; set; } = solarSystemVersion;
}