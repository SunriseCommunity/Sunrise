using CSharpFunctionalExtensions;
using osu.Shared;

namespace Sunrise.Shared.Utils;

public static class ModsValidationUtil
{
    // NOTE: Data from https://osu.ppy.sh/wiki/en/Gameplay/Game_modifier

    public static readonly List<Mods> InvalidMods = [Mods.Target, Mods.Random, Mods.KeyCoop, Mods.Cinema, Mods.Autoplay];

    public static readonly List<Mods> IgnoreMods = [Mods.None, Mods.TouchDevice, Mods.Relax, Mods.Relax2, Mods.ScoreV2];

    public static readonly List<Mods> DefaultDifficultyReductionMods =
    [
        Mods.Easy,
        Mods.NoFail,
        Mods.HalfTime
    ];

    public static readonly List<Mods> DefaultDifficultyIncreaseMods =
    [
        Mods.HardRock,
        Mods.SuddenDeath,
        Mods.Perfect,
        Mods.DoubleTime,
        Mods.Nightcore,
        Mods.Hidden,
        Mods.Flashlight
    ];

    public static readonly List<Mods> DefaultMods = DefaultDifficultyIncreaseMods.Concat(DefaultDifficultyReductionMods).ToList();

    public static readonly Dictionary<GameMode, List<Mods>> GameModesToAllowedMods = new()
    {
        {
            GameMode.Standard, new List<Mods>([Mods.SpunOut]).Concat(DefaultMods).ToList()
        },
        {
            GameMode.Taiko, DefaultMods
        },
        {
            GameMode.CatchTheBeat, DefaultMods
        },
        {
            GameMode.Mania, new List<Mods>([Mods.Key1, Mods.Key2, Mods.Key3, Mods.Key4, Mods.Key5, Mods.Key6, Mods.Key7, Mods.Key8, Mods.Key9, Mods.KeyCoop, Mods.FadeIn, Mods.Mirror, Mods.Random]).Concat(DefaultMods).ToList()
        }
    };

    public static readonly List<List<Mods>> ModsWithSinglePossibleInstance = new()
    {
        new List<Mods>([Mods.DoubleTime, Mods.HalfTime]),
        new List<Mods>([Mods.NoFail, Mods.SuddenDeath]),
        new List<Mods>([Mods.Key1, Mods.Key2, Mods.Key3, Mods.Key4, Mods.Key5, Mods.Key6, Mods.Key7, Mods.Key8, Mods.Key9]),
        new List<Mods>([Mods.Relax, Mods.Relax2, Mods.ScoreV2]), // Actually, ScoreV2 can be played with both Relax and Relax2 (Autopilot), but as we treat each mode differently, we only allow one at the time.
        new List<Mods>([Mods.Easy, Mods.HardRock])
    };

    public static Result ValidateMods(Mods mods, GameMode gameMode)
    {
        var hasInvalidMods = InvalidMods.Any(mod => mods.HasFlag(mod));

        if (hasInvalidMods)
            return Result.Failure("Score includes invalid mods");

        var allowedGameModeMods = GameModesToAllowedMods[gameMode];
        var nonIgnoredMods = mods & ~IgnoreMods.Aggregate(Mods.None, (current, mod) => current | mod);
        var hasInvalidModeCombination = nonIgnoredMods != Mods.None && !allowedGameModeMods.Any(mod => nonIgnoredMods.HasFlag(mod));

        if (hasInvalidModeCombination)
            return Result.Failure("Score includes mods that are not allowed for the game mode");

        var hasMultipleInstancesOfSingleInstanceMods = ModsWithSinglePossibleInstance.Any(modList =>
        {
            var count = modList.Count(mod => mods.HasFlag(mod));
            return count > 1;
        });

        if (hasMultipleInstancesOfSingleInstanceMods)
            return Result.Failure("Score includes multiple instances of single instance mods");

        return Result.Success();
    }
}