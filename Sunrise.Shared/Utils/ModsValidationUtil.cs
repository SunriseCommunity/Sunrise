using CSharpFunctionalExtensions;
using osu.Shared;

namespace Sunrise.Shared.Utils;

public static class ModsValidationUtil
{
    // NOTE: Data from https://osu.ppy.sh/wiki/en/Gameplay/Game_modifier

    public static readonly IReadOnlySet<Mods> InvalidMods = new HashSet<Mods>
    {
        Mods.Target,
        Mods.Random,
        Mods.KeyCoop,
        Mods.Cinema,
        Mods.Autoplay
    };

    public static readonly IReadOnlySet<Mods> IgnoredMods = new HashSet<Mods>
    {
        Mods.None,
        Mods.TouchDevice
    };

    public static readonly IReadOnlyDictionary<GameMode, IReadOnlyList<Mods>> SupportedMods =
        new Dictionary<GameMode, IReadOnlyList<Mods>>
        {
            [GameMode.Standard] =
            [
                Mods.Easy, Mods.NoFail, Mods.HalfTime, Mods.HardRock, Mods.SuddenDeath, Mods.Perfect,
                Mods.DoubleTime, Mods.Nightcore, Mods.Hidden, Mods.Flashlight, Mods.SpunOut
            ],
            [GameMode.Taiko] =
            [
                Mods.Easy, Mods.NoFail, Mods.HalfTime, Mods.HardRock, Mods.SuddenDeath, Mods.Perfect,
                Mods.DoubleTime, Mods.Nightcore, Mods.Hidden, Mods.Flashlight
            ],
            [GameMode.CatchTheBeat] =
            [
                Mods.Easy, Mods.NoFail, Mods.HalfTime, Mods.HardRock, Mods.SuddenDeath, Mods.Perfect,
                Mods.DoubleTime, Mods.Nightcore, Mods.Hidden, Mods.Flashlight
            ],
            [GameMode.Mania] =
            [
                Mods.Easy, Mods.NoFail, Mods.HalfTime, Mods.HardRock, Mods.SuddenDeath, Mods.Perfect,
                Mods.DoubleTime, Mods.Nightcore, Mods.Hidden, Mods.Flashlight,
                Mods.Key1, Mods.Key2, Mods.Key3, Mods.Key4, Mods.Key5, Mods.Key6, Mods.Key7, Mods.Key8, Mods.Key9,
                Mods.FadeIn, Mods.Mirror
            ]
        };

    public static readonly IReadOnlyDictionary<GameMode, IReadOnlyList<Mods>> SupportedModeChangingMods =
        new Dictionary<GameMode, IReadOnlyList<Mods>>
        {
            [GameMode.Standard] = [Mods.Relax, Mods.Relax2, Mods.ScoreV2],
            [GameMode.Taiko] = [Mods.Relax, Mods.ScoreV2],
            [GameMode.CatchTheBeat] = [Mods.Relax, Mods.ScoreV2],
            [GameMode.Mania] = [Mods.ScoreV2]
        };

    public static readonly IReadOnlyList<IReadOnlyList<Mods>> MutuallyExclusiveGroups =
    [
        [Mods.DoubleTime, Mods.HalfTime],
        [Mods.NoFail, Mods.SuddenDeath],
        [Mods.Key1, Mods.Key2, Mods.Key3, Mods.Key4, Mods.Key5, Mods.Key6, Mods.Key7, Mods.Key8, Mods.Key9],
        [Mods.Relax, Mods.Relax2, Mods.ScoreV2],
        [Mods.Easy, Mods.HardRock]
    ];

    public static Mods GetAllowedMask(GameMode gameMode)
    {
        return SupportedMods[gameMode]
            .Concat(SupportedModeChangingMods[gameMode])
            .Concat(IgnoredMods)
            .Aggregate(Mods.None, (mask, mod) => mask | mod);
    }

    public static Result ValidateMods(Mods mods, GameMode gameMode)
    {
        var hasInvalidMods = InvalidMods.Any(mod => mods.HasFlag(mod));

        if (hasInvalidMods)
            return Result.Failure("Score includes invalid mods");

        var hasInvalidModeCombination = (mods & ~GetAllowedMask(gameMode)) != Mods.None;

        if (hasInvalidModeCombination)
            return Result.Failure("Score includes mods that are not allowed for the game mode");

        var hasMultipleInstancesOfSingleInstanceMods = MutuallyExclusiveGroups.Any(modList =>
        {
            var count = modList.Count(mod => mods.HasFlag(mod));
            return count > 1;
        });

        if (hasMultipleInstancesOfSingleInstanceMods)
            return Result.Failure("Score includes multiple instances of single instance mods");

        return Result.Success();
    }
}