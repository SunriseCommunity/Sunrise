using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Pipeline;

public readonly record struct ScoreStateSnapshot(
    SubmissionStatus SubmissionStatus,
    bool IsScoreable,
    bool IsPassed,
    bool IsRanked)
{
    public static ScoreStateSnapshot Capture(Score score, BeatmapStatus beatmapStatus)
    {
        return new ScoreStateSnapshot(
            score.SubmissionStatus,
            beatmapStatus.IsScoreable(),
            score.IsPassed,
            beatmapStatus.IsRanked());
    }
}
