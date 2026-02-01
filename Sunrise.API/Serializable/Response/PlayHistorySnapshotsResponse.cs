using System.Text.Json.Serialization;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Serializable.Response;

public class PlayHistorySnapshotsResponse
{
    public PlayHistorySnapshotsResponse(Dictionary<DateTime, int> snapshots)
    {
        Snapshots = snapshots.Select(x => new PlayHistorySnapshotResponse
        {
            PlayCount = x.Value,
            SavedAt = x.Key
        }).ToList();

        TotalCount = snapshots.Count;
    }

    [JsonConstructor]
    public PlayHistorySnapshotsResponse()
    {
    }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("snapshots")]
    public List<PlayHistorySnapshotResponse> Snapshots { get; set; }
}

public class PlayHistorySnapshotResponse
{
    [JsonPropertyName("play_count")]
    public int PlayCount { get; set; }

    [JsonPropertyName("saved_at")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime SavedAt { get; set; }
}