using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Tests.Extensions;

public static class UserGradesExtensions
{
    public static void UpdateWithDbScore(this UserGrades userGrades, Score score)
    {
        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        if (isFailed || !score.IsScoreable)
            return;

        if (score.SubmissionStatus != SubmissionStatus.Best || !score.BeatmapStatus.IsRanked())
            return;

        switch (score.Grade)
        {
            case "XH":
                userGrades.CountXH++;
                break;
            case "X":
                userGrades.CountX++;
                break;
            case "SH":
                userGrades.CountSH++;
                break;
            case "S":
                userGrades.CountS++;
                break;
            case "A":
                userGrades.CountA++;
                break;
            case "B":
                userGrades.CountB++;
                break;
            case "C":
                userGrades.CountC++;
                break;
            case "D":
                userGrades.CountD++;
                break;
        }
    }
}