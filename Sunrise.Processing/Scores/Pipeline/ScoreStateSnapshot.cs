using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions.Beatmaps;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Pipeline;

public readonly record struct ScoreStateSnapshot(
    SubmissionStatus SubmissionStatus,
    bool IsScoreable,
    bool IsPassed,
    bool IsRanked)
{
    public static ScoreStateSnapshot Capture(Score score)
    {
        return new ScoreStateSnapshot(
            score.SubmissionStatus,
            score.IsScoreable,
            score.IsPassed,
            score.BeatmapStatus.IsRanked());
    }
}
