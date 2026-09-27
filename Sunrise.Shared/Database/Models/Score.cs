using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using osu.Shared;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Models;

[Table("score")]
[Index(nameof(UserId))]
[Index(nameof(UserId), nameof(BeatmapId))]
[Index(nameof(UserId), nameof(SubmissionStatus))]
[Index(nameof(GameMode), nameof(SubmissionStatus), nameof(WhenPlayed))]
[Index(nameof(BeatmapHash))]
[Index(nameof(UserId), nameof(BeatmapHash), nameof(GameMode))]
[Index(nameof(ScoreHash), IsUnique = true)]
public class Score
{
    public int Id { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public int UserId { get; set; }
    public int BeatmapId { get; set; }

    [MaxLength(32)]
    public string ScoreHash { get; set; }

    [MaxLength(255)]
    public string BeatmapHash { get; set; }

    public BeatmapHashStatus? BeatmapHashStatus { get; set; }

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
    public ScoreGrade Grade { get; set; }

    public bool IsPassed { get; set; }

    // TODO: Drop persisted IsScoreable once all score reads derive it from BeatmapStatus.
    public bool IsScoreable { get; set; }
    public SubmissionStatus SubmissionStatus { get; set; } = SubmissionStatus.Unknown;
    public GameMode GameMode { get; set; }
    public DateTime WhenPlayed { get; set; }
    public string OsuVersion { get; set; }
    public DateTime ClientTime { get; set; }
    public double Accuracy { get; set; }
    public double PerformancePoints { get; set; }
    public int TimeElapsed { get; set; }

    [ForeignKey(nameof(CalculationVersionId))]
    public CalculationVersion? CalculationVersion { get; set; }

    public int? CalculationVersionId { get; set; }

}
