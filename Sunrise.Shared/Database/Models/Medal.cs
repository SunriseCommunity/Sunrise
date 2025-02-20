using Sunrise.Shared.Enums;
using Watson.ORM.Core;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Models;

[Table("medal")]
public class Medal
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Nvarchar, 1024, false)]
    public string Name { get; set; }

    [Column(DataTypes.Nvarchar, 1024, false)]
    public string Description { get; set; }

    [Column(DataTypes.Int)]
    public GameMode? GameMode { get; set; }

    [Column(DataTypes.Int, false)]
    public MedalCategory Category { get; set; }

    [Column(DataTypes.Nvarchar, 1024)]
    public string? FileUrl { get; set; }

    [Column(DataTypes.Int)]
    public int FileId { get; set; }

    [Column(DataTypes.Nvarchar, 1024, false)]
    public string Condition { get; set; }
}