using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Serializable.Response;

public class StatsSnapshotsResponse
{
    public StatsSnapshotsResponse(List<StatsSnapshot> snapshots)
    {
        Snapshots = snapshots.Select(x => new StatsSnapshotResponse
        {
            CountryRank = x.CountryRank,
            PerformancePoints = x.PerformancePoints,
            Rank = x.Rank,
            SavedAt = x.SavedAt
        }).ToList();

        TotalCount = snapshots.Count;
    }

    [JsonConstructor]
    public StatsSnapshotsResponse()
    {
    }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("snapshots")]
    public List<StatsSnapshotResponse> Snapshots { get; set; }
}

public class StatsSnapshotResponse
{
    [JsonPropertyName("country_rank")]
    public long CountryRank { get; set; }

    [JsonPropertyName("pp")]
    public double PerformancePoints { get; set; }

    [JsonPropertyName("global_rank")]
    public long Rank { get; set; }

    [JsonPropertyName("saved_at")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime SavedAt { get; set; }
}