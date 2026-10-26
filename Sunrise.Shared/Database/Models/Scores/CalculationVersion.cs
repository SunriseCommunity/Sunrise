using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sunrise.Shared.Database.Models.Scores;

[Table("calculation_version")]
[Index(nameof(RosuVersion), nameof(SunriseRevision), IsUnique = true)]
public class CalculationVersion
{
    public const int CurrentSunriseRevision = 1;

    public int Id { get; set; }

    [MaxLength(64)]
    public required string RosuVersion { get; set; }

    public int SunriseRevision { get; set; }
}
