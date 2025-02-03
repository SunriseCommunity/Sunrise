using System.Text.Json;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models.Event;

[Table("event_user")]
public class EventUser
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int UserId { get; set; }

    [Column(DataTypes.Int, false)]
    public UserEventType EventType { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Ip { get; set; }

    [Column(DataTypes.Nvarchar, int.MaxValue, false)]
    public string JsonData { get; set; }

    [Column(DataTypes.DateTime, false)]
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