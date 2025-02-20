using Watson.ORM.Core;

namespace Sunrise.Shared.Database.Models;

[Table("medal_file")]
public class MedalFile
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int MedalId { get; set; }

    [Column(DataTypes.Nvarchar, 1024, false)]
    public string Path { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}