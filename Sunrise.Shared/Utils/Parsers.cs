using System.Security.Cryptography;
using System.Text;
using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Shared.Types.Enums;

namespace Sunrise.Shared.Utils;

public static class Parsers
{

    // TODO: Split everything here to extension methods

    public static string SecondsToString(int seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.ToString(@"mm\:ss");
    }

    public static string SecondsToMinutes(int seconds, bool showSeconds = false)
    {
        return seconds < 60 ? $"{seconds} second(s)" : $"{seconds / 60} minute(s) {(showSeconds ? $"{seconds % 60} second(s)" : "")}";
    }

    public static BeatmapStatusSearch GetBeatmapSearchStatus(string status)
    {
        var enumValue = Enum.TryParse(status, true, out BeatmapStatusSearch result) ? result : BeatmapStatusSearch.Pending;
        return enumValue;
    }

    public static BeatmapStatusSearch WebStatusToSearchStatus(int ranked)
    {
        return ranked switch
        {
            0 or 7 => BeatmapStatusSearch.Ranked,
            8 => BeatmapStatusSearch.Loved,
            3 => BeatmapStatusSearch.Qualified,
            2 => BeatmapStatusSearch.Pending,
            5 => BeatmapStatusSearch.Graveyard,
            _ => BeatmapStatusSearch.Any
        };
    }

    public static string? GetModsString(this Mods mods)
    {
        var shortedMods = string.Join("",
            Enum.GetValues<ModsShorted>()
                .Where(x => mods.HasFlag((Mods)x) && x != ModsShorted.None)
                .Where(x => !(mods.HasFlag(Mods.Nightcore) && x == (ModsShorted)Mods.DoubleTime))
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

    public static string ToHash(this string s)
    {
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(s))).ToLower();
    }

    public static string ToText(this BanchoUserStatus status)
    {
        return $"{status.Action} {status.ActionText}";
    }

    public static string CreateMD5(this string input)
    {
        var inputBytes = Encoding.ASCII.GetBytes(input);
        var hash = MD5.HashData(inputBytes);

        return Convert.ToHexString(hash).ToLower();
    }

    public static int ToSeconds(this TimeSpan time)
    {
        return (int)time.TotalSeconds;
    }

    public static bool IsSingleMod(this Mods flags)
    {
        return flags != Mods.None && (flags & flags - 1) == 0;
    }
}