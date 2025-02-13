using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models.User;

[Table("user_medals")]
public class UserMedals
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int UserId { get; set; }

    [Column(DataTypes.Int, false)]
    public int MedalId { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}