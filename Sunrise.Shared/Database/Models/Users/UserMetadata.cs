using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user_metadata")]
[Index(nameof(UserId))]
public class UserMetadata
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; }

    public UserPlaystyle Playstyle { get; set; } = UserPlaystyle.None;

    [StringLength(32)]
    public string Location { get; set; } = string.Empty;

    [StringLength(32)]
    public string Interest { get; set; } = string.Empty;

    [StringLength(32)]
    public string Occupation { get; set; } = string.Empty;

    [StringLength(32)]
    public string Telegram { get; set; } = string.Empty;

    [StringLength(32)]
    public string Twitch { get; set; } = string.Empty;

    [StringLength(32)]
    public string Twitter { get; set; } = string.Empty;

    [StringLength(32)]
    public string Discord { get; set; } = string.Empty;

    [StringLength(200)]
    public string Website { get; set; } = string.Empty;
}