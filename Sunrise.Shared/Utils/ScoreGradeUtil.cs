using osu.Shared;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects;
using GameMode = osu.Shared.GameMode;

namespace Sunrise.Shared.Utils;

// https://osu.ppy.sh/wiki/en/Gameplay/Grade
public static class ScoreGradeUtil
{
    public static bool TryParse(string value, out ScoreGrade grade) => Enum.TryParse(value, false, out grade) && Enum.IsDefined(grade);

    public static ScoreGrade Calculate(SubmittedScore score)
    {
        if (!score.IsPassed)
            return ScoreGrade.F;

        var grade = score.GameMode.ToVanillaGameMode() switch
        {
            GameMode.Standard => CalculateStandard(score),
            GameMode.Taiko => CalculateTaiko(score),
            GameMode.CatchTheBeat => CalculateAccuracyGrade(score.Accuracy, 98, 94, 90, 85),
            GameMode.Mania => CalculateAccuracyGrade(score.Accuracy, 95, 90, 80, 70),
            _ => throw new ArgumentOutOfRangeException(nameof(score.GameMode))
        };

        var silver = score.Mods.HasFlag(Mods.Hidden) || score.Mods.HasFlag(Mods.Flashlight) || score.Mods.HasFlag(Mods.FadeIn);
        return (grade, silver) switch
        {
            (ScoreGrade.X, true) => ScoreGrade.XH,
            (ScoreGrade.S, true) => ScoreGrade.SH,
            _ => grade
        };
    }

    private static ScoreGrade CalculateStandard(SubmittedScore score)
    {
        var total = score.Count300 + score.Count100 + score.Count50 + score.CountMiss;
        if (total == 0) return ScoreGrade.D;
        if (score.Count300 == total) return ScoreGrade.X;

        var ratio300 = (float)score.Count300 / total;
        var ratio50 = (float)score.Count50 / total;
        if (ratio300 > .9 && ratio50 <= .01 && score.CountMiss == 0) return ScoreGrade.S;
        if (ratio300 > .9 || ratio300 > .8 && score.CountMiss == 0) return ScoreGrade.A;
        if (ratio300 > .8 || ratio300 > .7 && score.CountMiss == 0) return ScoreGrade.B;
        return ratio300 > .6 ? ScoreGrade.C : ScoreGrade.D;
    }

    private static ScoreGrade CalculateTaiko(SubmittedScore score)
    {
        var total = score.Count300 + score.Count100 + score.CountMiss;
        if (total == 0) return ScoreGrade.D;
        if (score.Count300 == total) return ScoreGrade.X;

        var great = (float)score.Count300 / total;
        if (great > .9 && score.CountMiss == 0) return ScoreGrade.S;
        if (great > .9 || great > .8 && score.CountMiss == 0) return ScoreGrade.A;
        if (great > .8 || great > .7 && score.CountMiss == 0) return ScoreGrade.B;
        return great > .6 ? ScoreGrade.C : ScoreGrade.D;
    }

    private static ScoreGrade CalculateAccuracyGrade(double accuracy, double s, double a, double b, double c)
    {
        if (accuracy == 100) return ScoreGrade.X;
        return accuracy > s ? ScoreGrade.S : accuracy > a ? ScoreGrade.A : accuracy > b ? ScoreGrade.B : accuracy > c ? ScoreGrade.C : ScoreGrade.D;
    }
}
