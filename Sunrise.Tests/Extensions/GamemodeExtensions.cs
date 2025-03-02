using osu.Shared;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Tests.Extensions;

public static class GamemodeExtensions
{
    public static Mods GetGamemodeMods(this GameMode gamemode)
    {
        switch (gamemode)
        {
            case GameMode.Standard:
            case GameMode.Taiko:
            case GameMode.CatchTheBeat:
            case GameMode.Mania:
                return Mods.None;

            case GameMode.RelaxStandard:
            case GameMode.RelaxTaiko:
            case GameMode.RelaxCatchTheBeat:
                return Mods.Relax;

            case GameMode.AutopilotStandard:
                return Mods.Relax2;

            case GameMode.ScoreV2Standard:
            case GameMode.ScoreV2Taiko:
            case GameMode.ScoreV2CatchTheBeat:
            case GameMode.ScoreV2Mania:
                return Mods.ScoreV2;

            default:
                throw new ArgumentOutOfRangeException(nameof(gamemode), gamemode, null);
        }
    }
}