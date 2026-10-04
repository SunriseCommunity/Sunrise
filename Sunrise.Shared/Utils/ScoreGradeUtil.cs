using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects;
using InternalGameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using OsuGameMode = osu.Shared.GameMode;

namespace Sunrise.Shared.Utils;

// https://osu.ppy.sh/wiki/en/Gameplay/Grade
public static class ScoreGradeUtil
{
    public static bool TryParse(string value, out ScoreGrade grade) =>
        Enum.TryParse(value, false, out grade) && Enum.GetName(grade) == value;

    public static ScoreGrade Calculate(SubmittedScore score) =>
        Calculate(score.IsPassed, score.GameMode, score.Accuracy, score.Mods, score.Count300, score.Count100, score.Count50, score.CountMiss);

    public static ScoreGrade Calculate(Score score) =>
        Calculate(score.IsPassed, score.GameMode, score.Accuracy, score.Mods, score.Count300, score.Count100, score.Count50, score.CountMiss);

    private static ScoreGrade Calculate(bool isPassed, InternalGameMode gameMode, double accuracy, Mods mods,
        int count300, int count100, int count50, int countMiss)
    {
        if (!isPassed)
            return ScoreGrade.F;

        var grade = gameMode.ToVanillaGameMode() switch
        {
            OsuGameMode.Standard => CalculateStandard(count300, count100, count50, countMiss),
            OsuGameMode.Taiko => CalculateTaiko(count300, count100, countMiss),
            OsuGameMode.CatchTheBeat => CalculateAccuracyGrade(accuracy, 98, 94, 90, 85),
            OsuGameMode.Mania => CalculateAccuracyGrade(accuracy, 95, 90, 80, 70),
            _ => throw new ArgumentOutOfRangeException(nameof(gameMode))
        };

        var silver = mods.HasFlag(Mods.Hidden) || mods.HasFlag(Mods.Flashlight) || mods.HasFlag(Mods.FadeIn);
        return (grade, silver) switch
        {
            (ScoreGrade.X, true) => ScoreGrade.XH,
            (ScoreGrade.S, true) => ScoreGrade.SH,
            _ => grade
        };
    }

    private static ScoreGrade CalculateStandard(int count300, int count100, int count50, int countMiss)
    {
        var total = count300 + count100 + count50 + countMiss;
        if (total == 0) return ScoreGrade.D;
        if (count300 == total) return ScoreGrade.X;

        var ratio300 = (float)count300 / total;
        var ratio50 = (float)count50 / total;
        if (ratio300 > .9 && ratio50 <= .01 && countMiss == 0) return ScoreGrade.S;
        if (ratio300 > .9 || ratio300 > .8 && countMiss == 0) return ScoreGrade.A;
        if (ratio300 > .8 || ratio300 > .7 && countMiss == 0) return ScoreGrade.B;
        return ratio300 > .6 ? ScoreGrade.C : ScoreGrade.D;
    }

    private static ScoreGrade CalculateTaiko(int count300, int count100, int countMiss)
    {
        var total = count300 + count100 + countMiss;
        if (total == 0) return ScoreGrade.D;
        if (count300 == total) return ScoreGrade.X;

        var great = (float)count300 / total;
        if (great > .9 && countMiss == 0) return ScoreGrade.S;
        if (great > .9 || great > .8 && countMiss == 0) return ScoreGrade.A;
        if (great > .8 || great > .7 && countMiss == 0) return ScoreGrade.B;
        return great > .6 ? ScoreGrade.C : ScoreGrade.D;
    }

    private static ScoreGrade CalculateAccuracyGrade(double accuracy, double s, double a, double b, double c)
    {
        if (accuracy == 100) return ScoreGrade.X;
        return accuracy > s ? ScoreGrade.S : accuracy > a ? ScoreGrade.A : accuracy > b ? ScoreGrade.B : accuracy > c ? ScoreGrade.C : ScoreGrade.D;
    }
}
