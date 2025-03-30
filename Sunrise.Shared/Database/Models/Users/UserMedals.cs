using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user_medals")]
[Index(nameof(UserId))]
public class UserMedals
{
    public int Id { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; }

    public int UserId { get; set; }
    public int MedalId { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}