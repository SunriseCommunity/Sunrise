using osu.Shared;

namespace Sunrise.Server.Objects;

public class GetScoresRequest
{

    public GetScoresRequest(HttpRequest request)
    {

        var isModeParsed = Enum.TryParse<GameMode>(request.Query["m"], out var gameMode);

        if (!isModeParsed)
        {
            throw new Exception("Invalid request: Invalid mode");
        }

        Hash = request.Query["c"];
        BeatmapSetId = request.Query["i"];
        BeatmapName = request.Query["f"];
        Username = request.Query["us"];
        Mode = gameMode;
    }

    public string? Hash { get; set; }
    public GameMode Mode { get; set; }
    public string? BeatmapSetId { get; set; }
    public string? BeatmapName { get; set; }
    public string? Username { get; set; }

    public void ThrowIfHasEmptyFields()
    {
        if (string.IsNullOrEmpty(Hash) || string.IsNullOrEmpty(BeatmapName) ||
            string.IsNullOrEmpty(Username))
        {
            throw new Exception("Invalid request: Missing parameters");
        }
    }
}