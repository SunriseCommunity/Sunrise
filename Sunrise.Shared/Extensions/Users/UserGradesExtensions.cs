using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Shared.Extensions.Users;

public static class UserGradesExtensions
{
    public static void UpdateWithScore(this UserGrades userGrades, Score score, Score? prevScore = null)
    {
        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        if (isFailed || !score.IsScoreable || score.SubmissionStatus != SubmissionStatus.Best)
            return;

        if (prevScore != null)
            userGrades.UpdateUserGradesCount(prevScore, -1);

        userGrades.UpdateUserGradesCount(score, 1);
    }

    private static void UpdateUserGradesCount(this UserGrades userGrades, Score score, int delta)
    {
        switch (score.Grade)
        {
            case "XH": userGrades.CountXH = UpdateGradeCount(userGrades.CountXH, delta); break;
            case "X": userGrades.CountX = UpdateGradeCount(userGrades.CountX, delta); break;
            case "SH": userGrades.CountSH = UpdateGradeCount(userGrades.CountSH, delta); break;
            case "S": userGrades.CountS = UpdateGradeCount(userGrades.CountS, delta); break;
            case "A": userGrades.CountA = UpdateGradeCount(userGrades.CountA, delta); break;
            case "B": userGrades.CountB = UpdateGradeCount(userGrades.CountB, delta); break;
            case "C": userGrades.CountC = UpdateGradeCount(userGrades.CountC, delta); break;
            case "D": userGrades.CountD = UpdateGradeCount(userGrades.CountD, delta); break;
            case "F": break;
            default: throw new ArgumentOutOfRangeException($"Unknown grade: {score.Grade} while updating user grades with score.");
        }
    }

    private static int UpdateGradeCount(this int countGrade, int delta)
    {
        return Math.Max(0, countGrade + delta);
    }
}