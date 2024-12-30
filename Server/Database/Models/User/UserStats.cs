using Watson.ORM.Core;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Database.Models.User;

[Table("user_stats")]
public class UserStats
{
    public UserStats()
    {
        LocalProperties = new LocalProperties();
    }

    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int UserId { get; set; }

    [Column(DataTypes.Int, false)]
    public GameMode GameMode { get; set; }

    [Column(DataTypes.Double, 3, 2, false)]
    public double Accuracy { get; set; }

    [Column(DataTypes.Int, false)]
    public long TotalScore { get; set; }

    [Column(DataTypes.Int, false)]
    public long RankedScore { get; set; }

    [Column(DataTypes.Int, false)]
    public int PlayCount { get; set; }

    [Column(DataTypes.Double, int.MaxValue, int.MaxValue, false)]
    public double PerformancePoints { get; set; }

    [Column(DataTypes.Int, false)]
    public int MaxCombo { get; set; }

    [Column(DataTypes.Int, false)]
    public int PlayTime { get; set; }

    [Column(DataTypes.Int, false)]
    public int TotalHits { get; set; }

    [Column(DataTypes.Long)]
    public long? BestGlobalRank { get; set; }

    [Column(DataTypes.DateTime)]
    public DateTime? BestGlobalRankDate { get; set; }

    [Column(DataTypes.Long)]
    public long? BestCountryRank { get; set; }

    [Column(DataTypes.DateTime)]
    public DateTime? BestCountryRankDate { get; set; }

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