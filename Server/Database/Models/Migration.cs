using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models;

[Table("migration")]
public class Migration
{
    [Column(true, DataTypes.Int, false)] public int Id { get; set; }

    [Column(DataTypes.Nvarchar, 1024, false)]
    public string Name { get; set; }

    [Column(DataTypes.DateTime, false)] public DateTime AppliedAt { get; set; }
}