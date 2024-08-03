using osu.Shared;
using Watson.ORM.Core;

namespace Sunrise.Server.Objects.Models;

[Table("user")]
public class User
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }
    
    [Column(DataTypes.Nvarchar, 64, false)]
    public string Username { get; set; }
    
    [Column(DataTypes.Nvarchar, 64, false)]
    public string Passhash { get; set; }
    
    [Column(DataTypes.Int, false)]
    public short Country { get; set; }
    
    [Column(DataTypes.Int, false)]
    public PlayerRank Privilege { get; set; }
    
    [Column(DataTypes.DateTime, false)]
    public DateTime RegisterDate { get; set; } = DateTime.UtcNow;
}