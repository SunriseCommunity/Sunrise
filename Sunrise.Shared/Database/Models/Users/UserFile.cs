using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user_file")]
[Index(nameof(OwnerId))]
[Index(nameof(OwnerId), nameof(Type))]
public class UserFile
{
    public int Id { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public User User { get; set; }

    public int OwnerId { get; set; }
    public string Path { get; set; }
    public FileType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}