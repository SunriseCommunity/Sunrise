using System.ComponentModel.DataAnnotations.Schema;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace Sunrise.Shared.Database.Models.Scores;

[Table("score_metadata")]
[Index(nameof(ScoreHash))]
public class ScoreMetadata
{
    public int Id { get; set; }
    
    [ForeignKey(nameof(Id))]
    public Score Score { get; set; } = null!;

    public string ScoreHash { get; set; }
    public string OsuVersion { get; set; }
    public DateTime ClientTime { get; set; }
}