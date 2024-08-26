namespace Sunrise.Server.Objects;

public class SubmitScoreRequest(HttpRequest request)
{
    public string? ScoreEncoded { get; set; } = request.Form["score"];
    public IFormFile? Replay { get; set; } = request.Form.Files["score"];
    public string? OsuVersion { get; set; } = request.Form["osuver"];
    public string? Iv { get; set; } = request.Form["iv"];
    public string? PassHash { get; set; } = request.Form["pass"];
    public string? ScoreTime { get; set; } = request.Form["st"];
    public string? ScoreFailTime { get; set; } = request.Form["ft"];
    public string? BeatmapHash { get; set; } = request.Form["bmk"];
    public string? IsScoreNotComplete { get; set; } = request.Form["x"];

    public void ThrowIfHasEmptyFields()
    {
        if (string.IsNullOrEmpty(ScoreEncoded) || string.IsNullOrEmpty(OsuVersion) || string.IsNullOrEmpty(Iv) || string.IsNullOrEmpty(PassHash) || string.IsNullOrEmpty(IsScoreNotComplete) || string.IsNullOrEmpty(BeatmapHash) || Replay == null)
        {
            throw new Exception("Invalid request: Missing parameters");
        }
    }
}