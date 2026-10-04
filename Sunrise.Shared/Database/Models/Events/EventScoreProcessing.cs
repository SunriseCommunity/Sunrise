using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;

namespace Sunrise.Shared.Database.Models.Events;

[Table("event_score_processing")]
[Index(nameof(EventType))]
[Index(nameof(ScoreId))]
[Index(nameof(ExecutorId))]
public class EventScoreProcessing
{
    public int Id { get; set; }

    [ForeignKey(nameof(ExecutorId))]
    public User? Executor { get; set; }

    public int? ExecutorId { get; set; }

    [ForeignKey(nameof(ScoreId))]
    public Score? Score { get; set; }

    public int? ScoreId { get; set; }
    public int? TaskId { get; set; }

    public ScoreProcessingEventType EventType { get; set; }
    public string? JsonData { get; set; }
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