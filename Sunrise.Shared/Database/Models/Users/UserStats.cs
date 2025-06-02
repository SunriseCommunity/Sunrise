using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user_stats")]
[Index(nameof(UserId))]
[Index(nameof(UserId), nameof(GameMode), IsUnique = true)]
public class UserStats
{
    public UserStats()
    {
        LocalProperties = new LocalProperties();
    }

    public int Id { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; }

    public int UserId { get; set; }
    public GameMode GameMode { get; set; }
    public double Accuracy { get; set; }

    [Column(TypeName = "BIGINT")]
    public long TotalScore { get; set; }

    [Column(TypeName = "BIGINT")]
    public long RankedScore { get; set; }

    public int PlayCount { get; set; }
    public double PerformancePoints { get; set; }
    public int MaxCombo { get; set; }
    public int PlayTime { get; set; }
    public int TotalHits { get; set; }

    [Column(TypeName = "BIGINT")]
    public long? BestGlobalRank { get; set; }

    public DateTime? BestGlobalRankDate { get; set; }

    [Column(TypeName = "BIGINT")]
    public long? BestCountryRank { get; set; }

    public DateTime? BestCountryRankDate { get; set; }

    [NotMapped]
    public LocalProperties LocalProperties { get; set; }

    public UserStats Clone()
    {
        return (UserStats)MemberwiseClone();
    }
}

public class LocalProperties
{
    public long? Rank { get; set; }
}