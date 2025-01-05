using osu.Shared;
using Sunrise.Server.Helpers;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Extensions;

public static class GameModeExtensions
{
    public static osu.Shared.GameMode ToVanillaGameMode(this GameMode mode)
    {
        return (osu.Shared.GameMode)((int)mode % 4);
    }

    public static GameMode EnrichWithMods(this GameMode mode, Mods mods)
    {
        var gamemodeMode = SubmitScoreHelper.TryGetSelectedNotStandardMods(mods);

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
}