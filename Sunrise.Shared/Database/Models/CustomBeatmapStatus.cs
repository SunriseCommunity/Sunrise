using System.ComponentModel.DataAnnotations.Schema;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Database.Models;

[Table("custom_beatmap_status")]
public class CustomBeatmapStatus
{
    public int Id { get; set; }
    public required int BeatmapSetId { get; set; }
    public required string BeatmapHash { get; set; }


    [ForeignKey(nameof(UpdatedByUserId))]
    public User UpdatedByUser { get; set; }

    public required int UpdatedByUserId { get; set; }
    public required BeatmapStatus Status { get; set; }
}