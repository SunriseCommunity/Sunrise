using osu.Shared;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Scores;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Processing.Scores.Processors;

[TraceExecution]
public class UserGradesScoreProcessor(DatabaseService database) : ScoreEntityProcessorBase
{
    public override int Priority => 200;

    protected override Task OnNewSubmissionInternal(ScoreCommitContext ctx)
    {
        IncrementWithScore(ctx);
        return Task.CompletedTask;
    }

    protected override Task OnRecalculationInternal(ScoreCommitContext ctx)
    {
        return Task.CompletedTask;
    }

    protected override Task OnDeletionInternal(ScoreCommitContext ctx)
    {
        DecrementWithScore(ctx);
        return Task.CompletedTask;
    }

    protected override Task OnRestorationInternal(ScoreCommitContext ctx)
    {
        IncrementWithScore(ctx);
        return Task.CompletedTask;
    }

    protected override async Task AfterExecution(ScoreCommitContext ctx)
    {
        var updateUserGradesResult = await database.Users.Grades.UpdateUserGrades(ctx.UserGrades);
        if (updateUserGradesResult.IsFailure)
            throw new ApplicationException("Failed to persist user grades: " + updateUserGradesResult.Error);
    }

    private static void IncrementWithScore(ScoreCommitContext ctx)
    {
        var score = ctx.Score;
        var userGrades = ctx.UserGrades;
        var previousOverallBest = ctx.UserPersonalBestScores?.OverallPeer?.BestScoreByScoreValue;

        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);
        if (isFailed || !score.IsScoreable || score.SubmissionStatus != SubmissionStatus.Best)
            return;

        if (!IsOverallBestScore(score, previousOverallBest))
            return;

        if (previousOverallBest != null)
            UpdateUserGradesCount(userGrades, previousOverallBest.Grade, -1);

        UpdateUserGradesCount(userGrades, score.Grade, 1);
    }

    private static void DecrementWithScore(ScoreCommitContext ctx)
    {
        var score = ctx.Score;
        var userGrades = ctx.UserGrades;
        var original = ctx.OriginalState;
        var promotedOverallBest = ctx.UserPersonalBestScores?.OverallPeer?.BestScoreByScoreValue;

        var isFailed = !original.IsPassed && !score.Mods.HasFlag(Mods.NoFail);
        if (isFailed || !original.IsScoreable || original.SubmissionStatus != SubmissionStatus.Best)
            return;

        if (!IsOverallBestScore(score, promotedOverallBest))
            return;

        UpdateUserGradesCount(userGrades, score.Grade, -1);

        if (promotedOverallBest != null)
            UpdateUserGradesCount(userGrades, promotedOverallBest.Grade, 1);
    }

    private static bool IsOverallBestScore(Score score, Score? peer)
    {
        if (peer == null)
            return true;

        return new List<Score>
            {
                score,
                peer
            }
            .SortScoresByTheirScoreValue()
            .First() == score;
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