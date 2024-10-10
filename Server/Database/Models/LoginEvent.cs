using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models;

[Table("login_event")]
public class LoginEvent
{
    [Column(true, DataTypes.Int, false)] public int Id { get; set; }

    [Column(DataTypes.Int, false)] public int UserId { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Ip { get; set; }

    [Column(DataTypes.Nvarchar, int.MaxValue, false)]
    public string LoginData { get; set; }

    [Column(DataTypes.DateTime, false)] public DateTime Time { get; set; } = DateTime.UtcNow;
}