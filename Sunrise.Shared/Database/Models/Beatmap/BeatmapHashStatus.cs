using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Database.Models.Beatmap;

[Table("beatmap_hash_status")]
[Index(nameof(CheckedAt))]
public class BeatmapHashStatus
{
    public int Id { get; set; }

    [MaxLength(32)]
    public required string BeatmapHash { get; set; }

    public int BeatmapId { get; set; }
    public BeatmapStatus Status { get; set; }
    public DateTime CheckedAt { get; set; }
    public int MissCount { get; set; }
}
