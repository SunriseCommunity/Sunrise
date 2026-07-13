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

        var ratio300 = score.Count300 * 100d / total;
        var ratio50 = score.Count50 * 100d / total;
        return ratio300 switch
        {
            > 90 when ratio50 <= 1 && score.CountMiss == 0 => ScoreGrade.S,
            > 90 => ScoreGrade.A,
            > 80 when score.CountMiss == 0 => ScoreGrade.A,
            > 80 => ScoreGrade.B,
            > 70 when score.CountMiss == 0 => ScoreGrade.B,
            > 60 => ScoreGrade.C,
            _ => ScoreGrade.D
        };
    }

    private static ScoreGrade CalculateTaiko(SubmittedScore score)
    {
        var total = score.Count300 + score.Count100 + score.CountMiss;
        if (total == 0) return ScoreGrade.D;
        if (score.Count300 == total) return ScoreGrade.X;

        var great = score.Count300 * 100d / total;
        return great switch
        {
            > 90 when score.CountMiss == 0 => ScoreGrade.S,
            > 90 => ScoreGrade.A,
            > 80 when score.CountMiss == 0 => ScoreGrade.A,
            > 80 => ScoreGrade.B,
            > 70 when score.CountMiss == 0 => ScoreGrade.B,
            > 60 => ScoreGrade.C,
            _ => ScoreGrade.D
        };
    }

    private static ScoreGrade CalculateAccuracyGrade(double accuracy, double s, double a, double b, double c)
    {
        if (Math.Abs(accuracy - 100) < 0.0001) return ScoreGrade.X;
        return accuracy > s ? ScoreGrade.S : accuracy > a ? ScoreGrade.A : accuracy > b ? ScoreGrade.B : accuracy > c ? ScoreGrade.C : ScoreGrade.D;
    }
}
