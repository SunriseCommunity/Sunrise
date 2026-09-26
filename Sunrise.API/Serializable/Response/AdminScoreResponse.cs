using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Repositories;

namespace Sunrise.API.Serializable.Response;

public class AdminScoreResponse
{
    [JsonConstructor]
    public AdminScoreResponse()
    {
    }

    public AdminScoreResponse(SessionRepository sessionRepository, Score score)
    {
        Score = new ScoreResponse(sessionRepository, score);
        SubmissionStatus = score.SubmissionStatus;
        BeatmapStatus = score.BeatmapStatus;
        IsScoreable = score.IsScoreable;
        ScoreHash = score.ScoreHash;
    }

    [JsonPropertyName("score")]
    public ScoreResponse Score { get; set; }

    [JsonPropertyName("submission_status")]
    public SubmissionStatus SubmissionStatus { get; set; }

    [JsonPropertyName("beatmap_status")]
    public BeatmapStatus BeatmapStatus { get; set; }

    [JsonPropertyName("is_scoreable")]
    public bool IsScoreable { get; set; }

    [JsonPropertyName("score_hash")]
    public string? ScoreHash { get; set; }
}
