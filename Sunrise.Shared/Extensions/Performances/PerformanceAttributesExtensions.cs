using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Objects.Serializable.Performances;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Extensions.Performances;

public static class PerformanceAttributesExtensions
{
    public static PerformanceAttributes ApplyNotStandardModRecalculationsIfNeeded(this PerformanceAttributes performance, Score score)
    {
        if (score.Mods.HasFlag(Mods.Relax) && score.GameMode == GameMode.RelaxStandard)
        {
            performance.PerformancePoints = RecalculateToRelaxStdPerformance(performance, score.Accuracy, score.Mods);
        }

        if (score.Mods.HasFlag(Mods.Relax) && score.GameMode == GameMode.RelaxCatchTheBeat)
        {
            performance.PerformancePoints = RecalculateToRelaxCtbPerformance(performance, score.Mods);
        }

        if (score.Mods.HasFlag(Mods.Relax2) && score.GameMode == GameMode.AutopilotStandard)
        {
            performance.PerformancePoints = RecalculateToAutopilotStdPerformance(performance);
        }

        return performance;
    }

    public static PerformanceAttributes ApplyNotStandardModRecalculationsIfNeeded(this PerformanceAttributes performance, double accuracy, Mods mods)
    {
        if (mods.HasFlag(Mods.Relax) && performance.Difficulty.Mode == GameMode.Standard)
        {
            performance.PerformancePoints = RecalculateToRelaxStdPerformance(performance, accuracy, mods);
        }

        if (mods.HasFlag(Mods.Relax) && performance.Difficulty.Mode == GameMode.CatchTheBeat)
        {
            performance.PerformancePoints = RecalculateToRelaxCtbPerformance(performance, mods);
        }

        if (mods.HasFlag(Mods.Relax2) && performance.Difficulty.Mode == GameMode.Standard)
        {
            performance.PerformancePoints = RecalculateToAutopilotStdPerformance(performance);
        }

        return performance;
    }

    private static double RecalculateToRelaxStdPerformance(PerformanceAttributes performance, double accuracy, Mods mods)
    {
        var multi = CalculateStdPpMultiplier(performance);
        var streamsNerf = CalculateStreamsNerf(performance);

        double accDepression = 1;

        if (streamsNerf < 1.09)
        {
            var accFactor = (100 - accuracy) / 100;
            accDepression = Math.Max(0.86 - accFactor, 0.5);

            if (accDepression > 0.0)
            {
                performance.PerformancePointsAim *= accDepression;
            }
        }

        if (mods.HasFlag(Mods.HardRock))
        {
            multi *= Math.Min(2, Math.Max(1, 1 * (CalculateMissPenalty(performance) / 1.85)));
        }

        var relaxPp = Math.Pow(
            Math.Pow(performance.PerformancePointsAim ?? 0, 1.15) +
            Math.Pow(performance.PerformancePointsSpeed ?? 0, 0.65 * accDepression) +
            Math.Pow(performance.PerformancePointsAccuracy ?? 0, 1.1) +
            Math.Pow(performance.PerformancePointsFlashlight ?? 0, 1.13),
            1.0 / 1.1
        ) * multi;

        return double.IsNaN(relaxPp) ? 0.0 : relaxPp;
    }

    private static double RecalculateToAutopilotStdPerformance(PerformanceAttributes performance)
    {
        var multi = CalculateStdPpMultiplier(performance);

        var relaxPp = Math.Pow(
            Math.Pow(performance.PerformancePointsAim ?? 0, 0.6) +
            Math.Pow(performance.PerformancePointsSpeed ?? 0, 1.3) +
            Math.Pow(performance.PerformancePointsAccuracy ?? 0, 1.05) +
            Math.Pow(performance.PerformancePointsFlashlight ?? 0, 1.13),
            1.0 / 1.1
        ) * multi;

        return double.IsNaN(relaxPp) ? 0.0 : relaxPp;
    }

    private static double RecalculateToRelaxCtbPerformance(PerformanceAttributes performance, Mods mods)
    {
        if (mods.HasFlag(Mods.Easy))
        {
            performance.PerformancePoints *= 0.67;
        }

        return performance.PerformancePoints;
    }

    private static double CalculateMissPenalty(PerformanceAttributes performance)
    {
        var missCount = performance.State.Misses ?? 0;
        var diffStrainCount = performance.Difficulty.Aim ?? 0;

        if (diffStrainCount <= 0)
            return 0;

        var logValue = Math.Log(diffStrainCount);
        var denominatorPart = 4.0 * Math.Pow(logValue, 0.94);

        if (double.IsNaN(denominatorPart) || double.IsInfinity(denominatorPart))
            return 0;

        return 2.0 / (missCount / denominatorPart + 1.0);
    }

    private static double CalculateStreamsNerf(PerformanceAttributes performance)
    {
        var aimStrainValue = performance.Difficulty.AimDifficultStrainCount ?? 0;
        var speedStrainValue = performance.Difficulty.SpeedDifficultStrainCount ?? 0;

        return Math.Round(aimStrainValue / speedStrainValue * 100) / 100;
    }

    private static double CalculateStdPpMultiplier(PerformanceAttributes performance)
    {
        var aimValue = performance.PerformancePointsAim ?? 0;
        var speedValue = performance.PerformancePointsSpeed ?? 0;
        var accuracyValue = performance.PerformancePointsAccuracy ?? 0;
        var flashlightValue = performance.PerformancePointsFlashlight ?? 0;

        var ppValue = performance.PerformancePoints;

        // Reference: https://github.com/MaxOhn/rosu-pp/blob/51a303834fbf65f5c8c0a49061f3459c44f19d49/src/osu/performance/mod.rs#L850
        var sum = Math.Pow(
            Math.Pow(aimValue, 1.1) +
            Math.Pow(speedValue, 1.1) +
            Math.Pow(accuracyValue, 1.1) +
            Math.Pow(flashlightValue, 1.1),
            1.0 / 1.1
        );

        return ppValue / sum;
    }
}