using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.API.Serializable.Request;

public class EditUserPrivilegeRequest
{
    [JsonPropertyName("privilege")]
    [Required]
    public IEnumerable<UserPrivilege> Privilege { get; set; }
}
