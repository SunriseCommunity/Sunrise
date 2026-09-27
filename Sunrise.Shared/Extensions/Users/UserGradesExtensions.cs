using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;

namespace Sunrise.Shared.Extensions.Users;

public static class UserGradesExtensions
{
    public static void UpdateWithScore(this UserGrades userGrades, Score score, BeatmapStatus beatmapStatus, Score? prevScore = null)
    {
        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        if (isFailed || !beatmapStatus.IsScoreable() || score.SubmissionStatus != SubmissionStatus.Best)
            return;

        if (prevScore != null)
            userGrades.UpdateGradeCount(prevScore.Grade, -1);

        userGrades.UpdateGradeCount(score.Grade, 1);
    }
}
