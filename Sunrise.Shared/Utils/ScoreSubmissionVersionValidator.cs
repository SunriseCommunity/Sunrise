using System.Globalization;
using System.Text.RegularExpressions;
using Sunrise.Shared.Objects;

namespace Sunrise.Shared.Utils;

public static class ScoreSubmissionVersionValidator
{
    public static bool IsValid(string? value)
    {
        return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    public static bool MatchesClientVersion(string? serializedScoreVersion, string? formClientVersion)
    {
        if (!DateTime.TryParseExact(serializedScoreVersion, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scoreDate))
            return false;

        if (DateTime.TryParseExact(formClientVersion, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var legacyDate))
            return legacyDate == scoreDate;

        var versionMatch = Regex.Match(formClientVersion ?? string.Empty,
            @"^b(?<date>\d{8})(?:\.(?<revision>\d+))?(?:cuttingedge|beta)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!versionMatch.Success)
            return false;

        var revision = versionMatch.Groups["revision"];
        if (revision.Success && !int.TryParse(revision.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            return false;

        if (!DateTime.TryParseExact(versionMatch.Groups["date"].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var clientDate))
            return false;

        return OsuVersion.TryParse(formClientVersion!)?.Date == clientDate && clientDate == scoreDate;
    }
}
