using osu.Shared;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models;

[Table("score")]
public class Score
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int UserId { get; set; }

    [Column(DataTypes.Int, false)]
    public int BeatmapId { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string ScoreHash { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string BeatmapHash { get; set; }

    [Column(DataTypes.Int)]
    public int? ReplayFileId { get; set; }

    [Column(DataTypes.Int, false)]
    public int TotalScore { get; set; }

    [Column(DataTypes.Int, false)]
    public int MaxCombo { get; set; }

    [Column(DataTypes.Int, false)]
    public int Count300 { get; set; }

    [Column(DataTypes.Int, false)]
    public int Count100 { get; set; }

    [Column(DataTypes.Int, false)]
    public int Count50 { get; set; }

    [Column(DataTypes.Int, false)]
    public int CountMiss { get; set; }

    [Column(DataTypes.Int, false)]
    public int CountKatu { get; set; }

    [Column(DataTypes.Int, false)]
    public int CountGeki { get; set; }

    [Column(DataTypes.Boolean, false)]
    public bool Perfect { get; set; }

    [Column(DataTypes.Int, false)]
    public Mods Mods { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Grade { get; set; }

    [Column(DataTypes.Boolean, false)]
    public bool IsPassed { get; set; }

    /**
     * TODO: Add IsScoreable
     * Is true if the beatmap is ranked, approved or loved. False otherwise.
     */

    [Column(DataTypes.Boolean, false)]
    public bool IsRanked { get; set; }

    [Column(DataTypes.Int, false)]
    public GameMode GameMode { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime WhenPlayed { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string OsuVersion { get; set; }

    [Column(DataTypes.Int, false)]
    public BeatmapStatus BeatmapStatus { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime ClientTime { get; set; }

    [Column(DataTypes.Decimal, 100, 2, false)]
    public double Accuracy { get; set; }

    [Column(DataTypes.Decimal, 100, 2, false)]
    public double PerformancePoints { get; set; }
}