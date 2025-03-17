using osu.Shared;

namespace Sunrise.Shared.Extensions.Performances;

public static class ModsExtensions
{
    /// <summary>
    ///     Ignore needed mods for pp calculations.
    ///     For now, we do only custom recalculation for relax scores, thus this is the only mode which is ignored.
    /// </summary>
    /// <param name="mods"></param>
    /// <returns></returns>
    public static Mods IgnoreNotStandardModsForRecalculation(this Mods mods)
    {
        return mods & ~Mods.Relax;
    }
}