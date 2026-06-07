using System.Reflection;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Tests.Utils.Processing;

public static class ScoreCommitContextFactory
{
    public static ScoreCommitContext Create(
        ScoreTaskType taskType,
        Score score,
        User user,
        UserStats userStats,
        UserGrades userGrades,
        Beatmap? beatmap = null,
        BeatmapSet? beatmapSet = null,
        UserBeatmapPeers? userPersonalBestScores = null,
        ScoreStateSnapshot? originalState = null)
    {
        var context = new ScoreCommitContext(taskType, score, user, userStats, userGrades, beatmap, beatmapSet);

        if (userPersonalBestScores != null)
            SetUserPersonalBestScores(context, userPersonalBestScores);

        if (originalState.HasValue)
            SetOriginalState(context, originalState.Value);

        return context;
    }

    public static void SetOriginalState(ScoreCommitContext context, ScoreStateSnapshot originalState)
    {
        SetInternalProperty(context, nameof(ScoreCommitContext.OriginalState), originalState);
    }

    public static void SetUserPersonalBestScores(ScoreCommitContext context, UserBeatmapPeers? userPersonalBestScores)
    {
        SetInternalProperty(context, nameof(ScoreCommitContext.UserPersonalBestScores), userPersonalBestScores);
    }

    private static void SetInternalProperty(object instance, string propertyName, object? value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null)
            throw new InvalidOperationException($"Property {propertyName} was not found on {instance.GetType().Name}.");

        var setter = property.GetSetMethod(nonPublic: true);
        if (setter == null)
            throw new InvalidOperationException($"Property {propertyName} does not have a writable setter.");

        setter.Invoke(instance, [value]);
    }
}