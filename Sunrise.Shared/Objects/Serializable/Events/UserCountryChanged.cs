using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Objects.Serializable.Events;

public class UserCountryChanged
{
    [JsonPropertyName("NewCountry")]
    public CountryCode NewCountry { get; set; }
    
    [JsonPropertyName("OldCountry")]
    public CountryCode OldCountry { get; set; }
    
    [JsonPropertyName("UpdatedById")]
    public int UpdatedById { get; set; }
}