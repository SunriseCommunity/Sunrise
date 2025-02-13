using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models;

[Table("restriction")]
public class Restriction
{
    [Column(true, DataTypes.Int, false)] public int Id { get; set; }

    [Column(DataTypes.Int, false)] public int UserId { get; set; }

    [Column(DataTypes.Int, false)] public int ExecutorId { get; set; }

    [Column(DataTypes.Nvarchar, int.MaxValue, false)]
    public string Reason { get; set; }

    [Column(DataTypes.DateTime, false)] public DateTime Date { get; set; } = DateTime.UtcNow;

    [Column(DataTypes.DateTime, false)] public DateTime ExpiryDate { get; set; } = DateTime.MaxValue;
}