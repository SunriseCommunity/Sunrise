using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class EditUserRestrictionRequest
{
    [JsonPropertyName("is_restrict")]
    [Required]
    public required bool IsRestrict { get; set; }

    [JsonPropertyName("restriction_reason")]
    [MinLength(3)]
    [MaxLength(256)]
    public string? RestrictionReason { get; set; }
}