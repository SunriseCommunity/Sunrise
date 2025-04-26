using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user_favourite_beatmap")]
[Index(nameof(UserId), nameof(BeatmapSetId))]
[Index(nameof(UserId))]
public class UserFavouriteBeatmap
{
    public int Id { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; }

    public int UserId { get; set; }
    public int BeatmapSetId { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
}