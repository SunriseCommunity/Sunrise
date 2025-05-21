using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Database.Models.Events;

[Table("event_beatmap")]
[Index(nameof(BeatmapSetId))]
public class EventBeatmap
{
    public int Id { get; set; }
    public required int BeatmapSetId { get; set; }

    [ForeignKey("ExecutorId")]
    public User Executor { get; set; }

    public int ExecutorId { get; set; }

    public BeatmapEventType EventType { get; set; }
    public string? JsonData { get; set; } = null;
    public DateTime Time { get; set; } = DateTime.UtcNow;

    public void SetData<T>(T value)
    {
        JsonData = JsonSerializer.Serialize(value);
    }

    public T? GetData<T>()
    {
        if (string.IsNullOrEmpty(JsonData)) 
            return default;
        
        return JsonSerializer.Deserialize<T>(JsonData);
    }
}