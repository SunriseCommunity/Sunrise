using System.ComponentModel.DataAnnotations.Schema;

namespace Sunrise.Shared.Database.Models;

[Table("medal_file")]
public class MedalFile
{
    public int Id { get; set; }
    public string Path { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}