using osu.Shared;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using Watson.ORM.Core;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Models;

[Table("score")]
public class Score
{
    public Score()
    {
        LocalProperties = new LocalProperties().FromScore(this);
    }

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

    [Column(DataTypes.Long, false)]
    public long TotalScore { get; set; }

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

    [Column(DataTypes.Boolean, false)]
    public bool IsScoreable { get; set; }

    [Column(DataTypes.Int, false)]
    public SubmissionStatus SubmissionStatus { get; set; }

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

    public LocalProperties LocalProperties { get; set; }
}

public class LocalProperties
{
    /**
     * <summary>
     *     Simplifies some mods to their base form.
     *     <example>
     *         DTNC -> DT
     *     </example>
     * </summary>
     */
    public Mods SerializedMods { get; set; }

    public bool IsRanked { get; set; }
    public int LeaderboardPosition { get; set; }

    public LocalProperties FromScore(Score score)
    {
        SerializedMods = score.Mods & ~Mods.Nightcore;
        IsRanked = score.BeatmapStatus.IsRanked();
        LeaderboardPosition = -1;
        return this;
    }
}