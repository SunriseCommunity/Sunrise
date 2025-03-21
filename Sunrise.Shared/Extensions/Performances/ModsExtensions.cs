using osu.Shared;

namespace Sunrise.Shared.Extensions.Performances;

public static class ModsExtensions
{
    /// <summary>
    ///     Ignore needed mods for custom pp calculations.
    /// </summary>
    /// <param name="mods"></param>
    /// <returns></returns>
    public static Mods IgnoreNotStandardModsForRecalculation(this Mods mods)
    {
        return mods & ~Mods.Relax & ~Mods.Relax2;
    }
}