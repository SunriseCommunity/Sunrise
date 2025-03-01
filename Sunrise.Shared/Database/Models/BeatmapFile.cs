using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Index = Microsoft.EntityFrameworkCore.IndexAttribute;

namespace Sunrise.Shared.Database.Models;

[Table("beatmap_file")]
[Index(nameof(BeatmapId), IsUnique = true)]
[Index(nameof(BeatmapSetId))]
public class BeatmapFile
{
    public int Id { get; set; }
    public int BeatmapId { get; set; }
    public int BeatmapSetId { get; set; }
    public string Path { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}