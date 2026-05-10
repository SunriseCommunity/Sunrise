using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Users;

namespace Sunrise.Shared.Database.Models.Scores;

[Table("score_processing_queue")]
[Index(nameof(ScoreHash), IsUnique = true)]
public class ScoreProcessingQueue
{
    public int Id { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public int UserId { get; set; }

    [MaxLength(32)]
    public string ScoreHash { get; set; } = null!;

    public string ScoreSerialized { get; set; } = null!;
    public string BeatmapHash { get; set; } = null!;
    public int TimeElapsed { get; set; }
    public string OsuVersion { get; set; } = null!;
    public string ClientHash { get; set; } = null!;

    [ForeignKey(nameof(ReplayFileId))]
    public UserFile? ReplayFile { get; set; }

    public int? ReplayFileId { get; set; }
    public string? StoryboardHash { get; set; }
    public string UserHash { get; set; } = null!;
    public DateTime WhenPlayed { get; set; }
}