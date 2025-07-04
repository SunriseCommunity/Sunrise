using System.ComponentModel.DataAnnotations.Schema;

namespace Sunrise.Shared.Database.Models.Scores;

[Table("score_hits")]
public class ScoreHits
{
    public int Id { get; set; }
    
    [ForeignKey(nameof(Id))]
    public Score Score { get; set; } = null!;

    public int MaxCombo { get; set; }

    public int Count300 { get; set; }
    public int Count100 { get; set; }
    public int Count50 { get; set; }
    public int CountMiss { get; set; }
    public int CountKatu { get; set; }
    public int CountGeki { get; set; }
}