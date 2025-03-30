using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user_grades")]
[Index(nameof(UserId), nameof(GameMode), IsUnique = true)]
public class UserGrades
{
    public int Id { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    public required int UserId { get; set; }
    public required GameMode GameMode { get; set; }

    public int CountXH { get; set; } = 0;
    public int CountX { get; set; } = 0;
    public int CountSH { get; set; } = 0;
    public int CountS { get; set; } = 0;
    public int CountA { get; set; } = 0;
    public int CountB { get; set; } = 0;
    public int CountC { get; set; } = 0;
    public int CountD { get; set; } = 0;
}