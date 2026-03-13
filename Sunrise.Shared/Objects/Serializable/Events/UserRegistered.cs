using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Objects.Serializable.Events;

public class UserRegistered
{
    [JsonPropertyName("RegisterData")]
    public required UserRegisteredData RegisterData { get; set; }

    [JsonPropertyName("IsExemptFromMultiaccountCheck")]
    public bool? IsExemptFromMultiaccountCheck { get; set; }
}

public class UserRegisteredData
{
    [JsonPropertyName("Username")]
    public required string Username { get; set; }

    [JsonPropertyName("Email")]
    public required string Email { get; set; }

    [JsonPropertyName("Passhash")]
    public required string Passhash { get; set; }

    [JsonPropertyName("Country")]
    public CountryCode Country { get; set; }

    [JsonPropertyName("RegisterDate")]
    public DateTime RegisterDate { get; set; }
}
