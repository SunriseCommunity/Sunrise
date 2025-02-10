using System.Text.Json.Serialization;
using Sunrise.Server.API.Services;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Serializable.Response;

public class UserResponse
{

    [JsonConstructor]
    public UserResponse()
    {
    }

    public UserResponse(User user, string? status = null)
    {
        Id = user.Id;
        Username = user.Username;
        Description = user.Description;
        Country = ((CountryCodes)user.Country).ToString();
        RegisterDate = user.RegisterDate;
        LastOnlineTime = user.LastOnlineTime;
        IsRestricted = user.IsRestricted();
        SilencedUntil = user.SilencedUntil > DateTime.UtcNow ? user.SilencedUntil : null!;
        Badges = UserService.GetUserBadges(user);
        UserStatus = status;
    }



    [JsonPropertyName("user_id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("country_code")]
    public string Country { get; set; }

    [JsonPropertyName("register_date")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime RegisterDate { get; set; }

    [JsonPropertyName("last_online_time")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime LastOnlineTime { get; set; }

    [JsonPropertyName("restricted")]
    public bool IsRestricted { get; set; }

    [JsonPropertyName("silenced_until")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? SilencedUntil { get; set; }

    [JsonPropertyName("badges")]
    public List<string> Badges { get; set; }

    [JsonPropertyName("user_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserStatus { get; set; }
}