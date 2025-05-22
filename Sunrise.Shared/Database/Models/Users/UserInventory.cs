using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Enums;

namespace Sunrise.Shared.Database.Models.Users;

[Table("user_inventory_item")]
[Index(nameof(UserId), nameof(ItemType), IsUnique = true)]
public class UserInventoryItem
{
    public int Id { get; set; }

    public required int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; }

    public required ItemType ItemType { get; set; }

    public int Quantity { get; set; }
}