using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.API.Serializable.Response;

public class CustomBeatmapStatusChangeResponse
{
    public CustomBeatmapStatusChangeResponse(BeatmapResponse beatmap, BeatmapStatusWeb newStatus, BeatmapStatusWeb oldStatus, UserResponse batUser)
    {
        Beatmap = beatmap;
        User = batUser;
        NewStatus = newStatus;
        OldStatus = oldStatus;
    }

    [JsonConstructor]
    public CustomBeatmapStatusChangeResponse()
    {
    }

    [JsonPropertyName("beatmap")]
    public BeatmapResponse Beatmap { get; set; }

    [JsonPropertyName("new_status")]
    public BeatmapStatusWeb NewStatus { get; set; }

    [JsonPropertyName("old_status")]
    public BeatmapStatusWeb OldStatus { get; set; }

    [JsonPropertyName("bat")]
    public UserResponse User { get; set; }
}