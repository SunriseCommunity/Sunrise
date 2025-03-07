using osu.Shared;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Extensions.Scores;

public static class ModsExtensions
{
    public static string? GetModsString(this Mods mods)
    {
        var shortedMods = string.Join("",
            Enum.GetValues<ModsShorted>()
                .Where(x => mods.HasFlag((Mods)x) && x != ModsShorted.None)
                .Where(x => !(mods.HasFlag(Mods.Nightcore) && x == (ModsShorted)Mods.DoubleTime))
                .Where(x => !(mods.HasFlag(Mods.Perfect) && x == (ModsShorted)Mods.SuddenDeath))
                .Select(x => x.ToString()));

        return string.IsNullOrEmpty(shortedMods) ? string.Empty : $"+{shortedMods} ";
    }

    public static Mods StringModsToMods(this string shortedMods)
    {
        var dict = Enum.GetValues(typeof(ModsShorted))
            .Cast<ModsShorted>()
            .ToDictionary(t => t, t => t.ToString());

        var mods = dict.Where(kvp => shortedMods.Contains(kvp.Value, StringComparison.CurrentCultureIgnoreCase))
            .Aggregate(ModsShorted.None, (current, kvp) => current | kvp.Key);

        return (Mods)mods;
    }

    public static bool IsSingleMod(this Mods flags)
    {
        return flags != Mods.None && (flags & flags - 1) == 0;
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