using osu.Shared;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database.Models.Users;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Processors;

[TraceExecution]
public class UserGradesScoreProcessor : IScoreEntityProcessor
{
    public int Priority => 200;

    public Task OnNewSubmission(ScoreCommitContext ctx)
    {
        IncrementWithScore(ctx);
        return Task.CompletedTask;
    }

    public Task OnRecalculation(ScoreCommitContext ctx)
    {
        return Task.CompletedTask;
    }

    public Task OnDeletion(ScoreCommitContext ctx)
    {
        DecrementWithScore(ctx);
        return Task.CompletedTask;
    }

    public Task OnRestoration(ScoreCommitContext ctx)
    {
        IncrementWithScore(ctx);
        return Task.CompletedTask;
    }

    private static void IncrementWithScore(ScoreCommitContext ctx)
    {
        var score = ctx.Score;
        var userGrades = ctx.UserGrades;
        var prevBest = ctx.UserPersonalBestScores?.OverallPeer?.BestScoreBasedByTotalScore;

        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);
        if (isFailed || !score.IsScoreable || score.SubmissionStatus != SubmissionStatus.Best)
            return;

        if (prevBest != null)
            UpdateUserGradesCount(userGrades, prevBest.Grade, -1);

        UpdateUserGradesCount(userGrades, score.Grade, 1);
    }

    private static void DecrementWithScore(ScoreCommitContext ctx)
    {
        var score = ctx.Score;
        var userGrades = ctx.UserGrades;
        var original = ctx.OriginalState;

        var isFailed = !original.IsPassed && !score.Mods.HasFlag(Mods.NoFail);
        if (isFailed || !original.IsScoreable || original.SubmissionStatus != SubmissionStatus.Best)
            return;

        UpdateUserGradesCount(userGrades, score.Grade, -1);
    }

    private static void UpdateUserGradesCount(UserGrades userGrades, string grade, int delta)
    {
        switch (grade)
        {
            case "XH": userGrades.CountXH = Math.Max(0, userGrades.CountXH + delta); break;
            case "X": userGrades.CountX = Math.Max(0, userGrades.CountX + delta); break;
            case "SH": userGrades.CountSH = Math.Max(0, userGrades.CountSH + delta); break;
            case "S": userGrades.CountS = Math.Max(0, userGrades.CountS + delta); break;
            case "A": userGrades.CountA = Math.Max(0, userGrades.CountA + delta); break;
            case "B": userGrades.CountB = Math.Max(0, userGrades.CountB + delta); break;
            case "C": userGrades.CountC = Math.Max(0, userGrades.CountC + delta); break;
            case "D": userGrades.CountD = Math.Max(0, userGrades.CountD + delta); break;
            case "F": break;
            default: throw new ArgumentOutOfRangeException($"Unknown grade: {grade} while updating user grades with score.");
        }
    }
}