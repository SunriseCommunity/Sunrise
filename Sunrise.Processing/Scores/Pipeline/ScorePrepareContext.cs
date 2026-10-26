using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Processing.Scores.Pipeline;

public sealed class ScorePrepareContext(
    ScoreTaskType taskType,
    Score? untrackedScore = null,
    double? newScorePerformancePointsValue = null,
    Beatmap? beatmap = null,
    BeatmapSet? beatmapSet = null)
{
    public ScoreTaskType TaskType { get; } = taskType;
    public Score? UntrackedScore { get; } = untrackedScore;
    public double? NewScorePerformancePointsValue { get; } = newScorePerformancePointsValue;
    public Beatmap? Beatmap { get; } = beatmap;
    public BeatmapSet? BeatmapSet { get; } = beatmapSet;

    public static ScorePrepareContext FromCommitContext(ScoreCommitContext ctx)
    {
        return new ScorePrepareContext(ctx.TaskType, ctx.Score, ctx.Score.PerformancePoints, ctx.Beatmap, ctx.BeatmapSet);
    }
}
