using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums.Scores;

namespace Sunrise.Shared.Database.Models.Scores;

[Table("calculation_run")]
[Index(nameof(FinishedAt))]
public class CalculationRun
{
    public int Id { get; set; }

    [ForeignKey(nameof(TargetVersionId))]
    public CalculationVersion? TargetVersion { get; set; }

    public int TargetVersionId { get; set; }
    public CalculationRunPhase Phase { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public bool IsSuperseded { get; set; }
    public bool IsForced { get; set; }
}
