using osu.Shared;
using Watson.ORM.Core;

namespace Sunrise.Database.Schemas;

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

    [Column(DataTypes.Decimal, maxLength: 100, precision: 2, false)]
    public float Accuracy { get; set; } = 0;

    [Column(DataTypes.Double, maxLength: 45, precision: 2, false)]
    public long TotalScore { get; set; } = 0;

    [Column(DataTypes.Double, maxLength: 45, precision: 2, false)]
    public long RankedScore { get; set; } = 0;

    [Column(DataTypes.Int, false)]
    public int PlayCount { get; set; } = 0;

    [Column(DataTypes.Int, false)]
    public short PerformancePoints { get; set; } = 0;

    [Column(DataTypes.Int, false)]
    public int PlayTime { get; set; } = 0;

    [Column(DataTypes.DateTime, false)]
    public DateTime RegisterDate { get; set; } = DateTime.Now;

    public User()
    {
    }

    public User SetUserStats(string username, string passhash, short country, PlayerRank privilege)
    {
        Username = username;
        Passhash = passhash;
        Country = country;
        Privilege = privilege;
        RegisterDate = DateTime.Now;
        return this;
    }
}