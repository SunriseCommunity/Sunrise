using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Database.Models.Events;

[Table("event_user")]
[Index(nameof(EventType), nameof(UserId))]
[Index(nameof(EventType), nameof(Ip))]
public class EventUser
{
    public int Id { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; }

    public int UserId { get; set; }
    public UserEventType EventType { get; set; }
    public string Ip { get; set; }
    public string JsonData { get; set; }
    public DateTime Time { get; set; } = DateTime.UtcNow;

    public void SetData<T>(T value)
    {
        JsonData = JsonSerializer.Serialize(value);
    }

    public T? GetData<T>()
    {
        return JsonSerializer.Deserialize<T>(JsonData);
    }
}