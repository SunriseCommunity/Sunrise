using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Users;

namespace Sunrise.Shared.Database.Models.Beatmap;

[Table("beatmap_hype")]
[Index(nameof(BeatmapSetId), nameof(UserId), IsUnique = true)]
[Index(nameof(BeatmapSetId), nameof(Hypes))]
public class BeatmapHype
{
    public int Id { get; set; }
    public required int BeatmapSetId { get; set; }

    public required int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; }

    public int Hypes { get; set; } = 0;
}