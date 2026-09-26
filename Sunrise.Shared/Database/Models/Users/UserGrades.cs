using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;

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

    public void UpdateGradeCount(ScoreGrade grade, int delta)
    {
        if (delta is > 1 or < -1)
            throw new ArgumentOutOfRangeException(nameof(delta));

        switch (grade)
        {
            case ScoreGrade.XH: CountXH = Math.Max(0, CountXH + delta); break;
            case ScoreGrade.X: CountX = Math.Max(0, CountX + delta); break;
            case ScoreGrade.SH: CountSH = Math.Max(0, CountSH + delta); break;
            case ScoreGrade.S: CountS = Math.Max(0, CountS + delta); break;
            case ScoreGrade.A: CountA = Math.Max(0, CountA + delta); break;
            case ScoreGrade.B: CountB = Math.Max(0, CountB + delta); break;
            case ScoreGrade.C: CountC = Math.Max(0, CountC + delta); break;
            case ScoreGrade.D: CountD = Math.Max(0, CountD + delta); break;
            case ScoreGrade.F: break;
            default: throw new ArgumentOutOfRangeException($"Unknown grade: {grade} while updating user grades.");
        }
    }

    public int GetGradeCount(ScoreGrade grade)
    {
        return grade switch
        {
            ScoreGrade.XH => CountXH,
            ScoreGrade.X => CountX,
            ScoreGrade.SH => CountSH,
            ScoreGrade.S => CountS,
            ScoreGrade.A => CountA,
            ScoreGrade.B => CountB,
            ScoreGrade.C => CountC,
            ScoreGrade.D => CountD,
            _ => throw new ArgumentOutOfRangeException($"Unknown grade: {grade} while getting")
        };
    }
}
