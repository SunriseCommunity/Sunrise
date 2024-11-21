using System.Text.Json;
using osu.Shared;
using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models.User;

[Table("user_stats_snapshot")]
public class UserStatsSnapshot
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int UserId { get; set; }

    [Column(DataTypes.Int, false)]
    public GameMode GameMode { get; set; }

    [Column(DataTypes.Nvarchar, int.MaxValue, false)]
    public string SnapshotsJson { get; set; } = "[]";

    public void SetSnapshots(List<StatsSnapshot> value)
    {
        SnapshotsJson = JsonSerializer.Serialize(value);
    }

    public List<StatsSnapshot> GetSnapshots()
    {
        return JsonSerializer.Deserialize<List<StatsSnapshot>>(SnapshotsJson);
    }
}

public class StatsSnapshot
{
    public long Rank { get; set; }
    public long CountryRank { get; set; }
    public double PerformancePoints { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}