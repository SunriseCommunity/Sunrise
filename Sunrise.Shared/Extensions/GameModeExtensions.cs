using osu.Shared;
using GameMode = Sunrise.Shared.Types.Enums.GameMode;

namespace Sunrise.Shared.Extensions;

public static class GameModeExtensions
{
    public static osu.Shared.GameMode ToVanillaGameMode(this GameMode mode)
    {
        return (osu.Shared.GameMode)((int)mode % 4);
    }

    public static GameMode EnrichWithMods(this GameMode mode, Mods mods)
    {
        var gamemodeMode = mods.TryGetSelectedNotStandardMods();

        if (gamemodeMode != Mods.None)
        {
            switch (gamemodeMode)
            {
                case Mods.ScoreV2:
                    mode += 12;
                    break;
                case Mods.Relax:
                    mode += 4;
                    break;
                case Mods.Relax2:
                    mode += 8;
                    break;
            }
        }

        if (!Enum.IsDefined(typeof(GameMode), mode))
            mode = (GameMode)mode.ToVanillaGameMode();

        return mode;
    }

    public static bool IsGameModeWithoutScoreMultiplier(this GameMode mode)
    {
        var isRelax = mode is GameMode.RelaxStandard or GameMode.RelaxTaiko or GameMode.RelaxCatchTheBeat;
        var isAutopilot = mode == GameMode.AutopilotStandard;

        return isRelax || isAutopilot;
    }

    /// <summary>
    ///     This method checks if the score has any non-standard mods.
    ///     If the score has any of the following mods, it will be considered as non-standard:
    ///     <see cref="Mods.ScoreV2" />, <see cref="Mods.Relax" />, <see cref="Mods.Relax2" />
    /// </summary>
    /// <param name="mods"></param>
    /// <returns></returns>
    public static Mods TryGetSelectedNotStandardMods(this Mods mods)
    {
        Mods[] prioritizedMods =
        [
            Mods.ScoreV2,
            Mods.Relax,
            Mods.Relax2
        ];

        return prioritizedMods.Where(mod => mods.HasFlag(mod)).Aggregate(Mods.None, (current, mod) => current | mod);
    }
}