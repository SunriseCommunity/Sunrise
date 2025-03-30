using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user_stats_snapshot")]
[Index(nameof(UserId), nameof(GameMode))]
public class UserStatsSnapshot
{
    public int Id { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; }

    public int UserId { get; set; }
    public GameMode GameMode { get; set; }
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