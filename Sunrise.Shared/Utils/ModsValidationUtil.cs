using osu.Shared;

namespace Sunrise.Processing.Utils;

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

    // TODO: Validate
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
        //new List<Mods>([Mods.NoFail, Mods.SuddenDeath, Mods.Perfect]) // TODO: Double check
        new List<Mods>([Mods.Key1, Mods.Key2, Mods.Key3, Mods.Key4, Mods.Key5, Mods.Key6, Mods.Key7, Mods.Key8, Mods.Key9]),
        new List<Mods>([Mods.Relax, Mods.Relax2, Mods.Autoplay]),
        new List<Mods>([Mods.Easy, Mods.HardRock]),
        new List<Mods>([Mods.Hidden, Mods.Flashlight])
        // TODO: Maybe need to add more here
    };

    public static bool IsModeCombinationInvalid(Mods mods, GameMode gameMode)
    {
        var hasInvalidMods = InvalidMods.Any(mod => mods.HasFlag(mod));

        var allowedMods = GameModesToAllowedMods[gameMode];
        var nonIgnoredMods = mods & ~IgnoreMods.Aggregate(Mods.None, (current, mod) => current | mod);
        var hasInvalidModeCombination = nonIgnoredMods != Mods.None && !allowedMods.Any(mod => nonIgnoredMods.HasFlag(mod));

        var hasMultipleInstancesOfSingleInstanceMods = ModsWithSinglePossibleInstance.Any(modList =>
        {
            var count = modList.Count(mod => mods.HasFlag(mod));
            return count > 1;
        });

        return hasInvalidModeCombination || hasInvalidMods || hasMultipleInstancesOfSingleInstanceMods;
    }
}