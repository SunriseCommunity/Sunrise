using Watson.ORM.Core;

namespace Sunrise.Shared.Database.Models;

// TODO: Can we delete this?
[Obsolete("Writing migrations manually is obsolete, this class only exists for easy migration to EF from WatsonORM")]
[Table("migration")]
public class Migration
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Nvarchar, 1024, false)]
    public string Name { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime AppliedAt { get; set; }
}