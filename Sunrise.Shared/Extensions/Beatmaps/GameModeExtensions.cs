using osu.Shared;
using Sunrise.Shared.Extensions.Scores;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Extensions.Beatmaps;

public static class GameModeExtensions
{
    public static osu.Shared.GameMode ToVanillaGameMode(this GameMode mode)
    {
        return (osu.Shared.GameMode)((int)mode % 4);
    }
    
    public static bool IsVanillaGameMode(this GameMode mode)
    {
        return mode is GameMode.Taiko or GameMode.Mania or GameMode.Standard or GameMode.CatchTheBeat;
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

    public static List<GameMode> GetGameModesWithoutScoreMultiplier()
    {
        return [GameMode.RelaxStandard, GameMode.RelaxTaiko, GameMode.RelaxCatchTheBeat, GameMode.AutopilotStandard];
    }
}