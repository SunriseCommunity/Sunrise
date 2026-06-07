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
            UpdateUserGradesCount(userGrades, prevScore, -1);

        UpdateUserGradesCount(userGrades, score, 1);
    }

    private static void UpdateUserGradesCount(UserGrades userGrades, Score score, int delta)
    {
        switch (score.Grade)
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
            default: throw new ArgumentOutOfRangeException($"Unknown grade: {score.Grade} while updating user grades with score.");
        }
    }
}