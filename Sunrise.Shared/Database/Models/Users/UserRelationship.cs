using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user_relationship")]
[Index(nameof(UserId), nameof(TargetId))]
public class UserRelationship
{
    public int Id { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; }

    public int UserId { get; set; }

    [ForeignKey("TargetId")]
    public User Target { get; set; }

    public int TargetId { get; set; }

    public UserRelation Relation { get; set; } = UserRelation.None;
}