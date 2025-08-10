using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.API.Serializable.Request;

public class CountryChangeRequest
{
    [JsonPropertyName("new_country")]
    [EnumDataType(typeof(CountryCode))]
    [Required]
    public required CountryCode NewCountry { get; set; }
}