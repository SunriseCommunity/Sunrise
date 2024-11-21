using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models;

[Table("user_favourite_beatmap")]
public class UserFavouriteBeatmap
{
    [Column(true, DataTypes.Int, false)] public int Id { get; set; }

    [Column(DataTypes.Int, false)] public int UserId { get; set; }

    [Column(DataTypes.Int, false)] public int BeatmapSetId { get; set; }

    [Column(DataTypes.DateTime, false)] public DateTime DateAdded { get; set; } = DateTime.UtcNow;
}