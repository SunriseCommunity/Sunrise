using System.Text.Json.Serialization;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Objects.Serializable;

public class BeatmapSet
{
    private readonly Dictionary<string, BeatmapStatus> _statusMap = new()
    {
        ["loved"] = BeatmapStatus.Loved,
        ["qualified"] = BeatmapStatus.Qualified,
        ["approved"] = BeatmapStatus.Approved,
        ["ranked"] = BeatmapStatus.Ranked,
        ["pending"] = BeatmapStatus.Pending,
        ["graveyard"] = BeatmapStatus.Pending
    };

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("artist")]
    public string Artist { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("creator")]
    public string Creator { get; set; }

    [JsonPropertyName("status")]
    public string StatusString { get; set; }

    public BeatmapStatus Status => _statusMap[StatusString ?? "graveyard"];

    [JsonPropertyName("last_updated")]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("ranked_date")]
    public DateTime? RankedDate { get; set; }

    [JsonPropertyName("video")]
    public bool HasVideo { get; set; }

    [JsonPropertyName("beatmaps")]
    public Beatmap[] Beatmaps { get; set; }
}