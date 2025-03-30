using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Models;

[Table("medal")]
[Index(nameof(Category))]
[Index(nameof(GameMode))]
public class Medal
{
    public int Id { get; set; }

    public required string Name { get; set; }
    public required string Description { get; set; }
    public GameMode? GameMode { get; set; }
    public MedalCategory Category { get; set; }
    public string? FileUrl { get; set; }

    [ForeignKey("FileId")]
    public MedalFile? File { get; set; }

    public int? FileId { get; set; }
    public required string Condition { get; set; }
}