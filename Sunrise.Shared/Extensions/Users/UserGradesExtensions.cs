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
            userGrades.UpdateGradeCount(prevScore.Grade, -1);

        userGrades.UpdateGradeCount(score.Grade, 1);
    }
}
