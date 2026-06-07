using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Processing.Scores.Pipeline;

public sealed class ScoreCommitContext(
    ScoreTaskType taskType,
    Score score,
    User user,
    UserStats userStats,
    UserGrades userGrades,
    Beatmap? beatmap = null,
    BeatmapSet? beatmapSet = null)
{
    public ScoreTaskType TaskType { get; } = taskType;
    public ScoreStateSnapshot OriginalState { get; internal set; }
    public UserBeatmapPeers? UserPersonalBestScores { get; internal set; }
    public List<Medal>? UnlockedMedals { get; internal set; }

    public Score Score { get; } = score;
    public User User { get; } = user;
    public UserStats UserStats { get; } = userStats;
    public UserGrades UserGrades { get; } = userGrades;
    public Beatmap? Beatmap { get; } = beatmap;
    public BeatmapSet? BeatmapSet { get; } = beatmapSet;
}