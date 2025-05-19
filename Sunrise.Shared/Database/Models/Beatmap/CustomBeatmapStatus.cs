using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Database.Models.Beatmap;

[Table("custom_beatmap_status")]
[Index(nameof(BeatmapHash))]
[Index(nameof(BeatmapSetId))]
public class CustomBeatmapStatus
{
    public int Id { get; set; }
    public required int BeatmapSetId { get; set; }
    public required string BeatmapHash { get; set; }

    [ForeignKey(nameof(UpdatedByUserId))]
    public User UpdatedByUser { get; set; }

    public required int UpdatedByUserId { get; set; }
    public required BeatmapStatusWeb Status { get; set; }
}