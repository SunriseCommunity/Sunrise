using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using osu.Shared;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Models;

[Table("score")]
[Index(nameof(UserId))]
[Index(nameof(UserId), nameof(BeatmapId))]
[Index(nameof(UserId), nameof(SubmissionStatus), nameof(BeatmapStatus))]
[Index(nameof(BeatmapId), nameof(IsScoreable), nameof(IsPassed), nameof(SubmissionStatus))]
[Index(nameof(GameMode), nameof(SubmissionStatus), nameof(BeatmapStatus), nameof(WhenPlayed))]
[Index(nameof(BeatmapHash))]
public class Score
{
    public Score()
    {
        LocalProperties = new LocalProperties().FromScore(this);
    }

    public int Id { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; }

    public int UserId { get; set; }
    public int BeatmapId { get; set; }
    public string ScoreHash { get; set; }
    public string BeatmapHash { get; set; }

    [ForeignKey("ReplayFileId")]
    public UserFile? ReplayFile { get; set; }

    public int? ReplayFileId { get; set; }

    [Column(TypeName = "BIGINT")]
    public long TotalScore { get; set; }

    public int MaxCombo { get; set; }
    public int Count300 { get; set; }
    public int Count100 { get; set; }
    public int Count50 { get; set; }
    public int CountMiss { get; set; }
    public int CountKatu { get; set; }
    public int CountGeki { get; set; }
    public bool Perfect { get; set; }
    public Mods Mods { get; set; }
    public string Grade { get; set; }
    public bool IsPassed { get; set; }
    public bool IsScoreable { get; set; }
    public SubmissionStatus SubmissionStatus { get; set; } = SubmissionStatus.Unknown;
    public GameMode GameMode { get; set; }
    public DateTime WhenPlayed { get; set; }
    public string OsuVersion { get; set; }
    public BeatmapStatus BeatmapStatus { get; set; }
    public DateTime ClientTime { get; set; }
    public double Accuracy { get; set; }
    public double PerformancePoints { get; set; }

    [NotMapped]
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
    public int? LeaderboardPosition { get; set; }

    public LocalProperties FromScore(Score score)
    {
        SerializedMods = score.Mods & ~Mods.Nightcore;
        IsRanked = score.BeatmapStatus.IsRanked();
        return this;
    }
}