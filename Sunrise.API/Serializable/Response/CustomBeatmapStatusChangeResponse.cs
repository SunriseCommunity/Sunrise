using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.API.Serializable.Response;

public class CustomBeatmapStatusChangeResponse
{
    public CustomBeatmapStatusChangeResponse(BeatmapResponse beatmap, BeatmapStatus newStatus, BeatmapStatus oldStatus, UserResponse batUser)
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
    public BeatmapStatus NewStatus { get; set; }

    [JsonPropertyName("old_status")]
    public BeatmapStatus OldStatus { get; set; }

    [JsonPropertyName("bat")]
    public UserResponse User { get; set; }
}