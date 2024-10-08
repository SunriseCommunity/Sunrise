using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models;

[Table("beatmap_file")]
public class BeatmapFile
{
    [Column(true, DataTypes.Int, false)] public int Id { get; set; }

    [Column(DataTypes.Int, false)] public int BeatmapId { get; set; }

    [Column(DataTypes.Int, false)] public int BeatmapSetId { get; set; }

    [Column(DataTypes.Nvarchar, int.MaxValue, false)]
    public string Path { get; set; }

    [Column(DataTypes.DateTime, false)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}