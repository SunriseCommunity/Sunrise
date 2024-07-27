using Sunrise.GameClient.Types.Enums;
using Watson.ORM.Core;

namespace Sunrise.Database.Schemas;

[Table("file")]
public class File
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int OwnerId { get; set; }

    [Column(DataTypes.Nvarchar, 255, false)]
    public string Path { get; set; }

    [Column(DataTypes.Nvarchar, 255, false)]
    public FileType Type { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime CreatedAt { get; set; }
}
