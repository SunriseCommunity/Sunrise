using System.ComponentModel.DataAnnotations.Schema;
using Sunrise.Shared.Database.Models.Users;

namespace Sunrise.Shared.Database.Models;

[Table("restriction")]
public class Restriction
{
    public int Id { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; }

    public int UserId { get; set; }

    [ForeignKey("ExecutorId")]
    public User? Executor { get; set; }

    public int? ExecutorId { get; set; }
    public string Reason { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public DateTime ExpiryDate { get; set; } = DateTime.MaxValue;
}