using System.Globalization;
using System.Text.RegularExpressions;

namespace Sunrise.Shared.Objects;

public partial class OsuVersion
{
    private const string VersionPrefix = "b";

    public static readonly string[] SupportedStreams = ["stable40", "cuttingedge"];
    public DateTime Date { get; init; }
    public int Revision { get; init; }
    public string Stream { get; init; } = null!;

    public static OsuVersion? TryParse(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString) || !versionString.StartsWith(VersionPrefix))
            return null;

        var raw = versionString[VersionPrefix.Length..];

        var stream = "stable40";

        if (raw.EndsWith("cuttingedge", StringComparison.OrdinalIgnoreCase))
        {
            stream = "cuttingedge";
            raw = raw[..^"cuttingedge".Length];
        }
        
        if (raw.EndsWith("beta", StringComparison.OrdinalIgnoreCase))
        {
            stream = "stable40";
            raw = raw[..^"beta".Length]; // Beta is discontinued, so we are going to treat it as stable40 for the sake of simplicity.
        }

        return ParseFromRaw(stream, raw);
    }

    public static OsuVersion? Parse(string stream, string versionString)
    {
        return string.IsNullOrWhiteSpace(versionString) ? null : ParseFromRaw(stream, versionString);
    }

    private static OsuVersion? ParseFromRaw(string stream, string rawVersion)
    {
        var split = rawVersion.Split('.');
        var dateString = split[0].Length >= 8 ? split[0][..8] : split[0];

        if (!DateTime.TryParseExact(dateString, "yyyyMMdd", null, DateTimeStyles.None, out var date))
            return null;

        var revision = split.Length > 1 ? ExtractLeadingInt(split[1]) : 0;

        return new OsuVersion
        {
            Date = date,
            Revision = revision,
            Stream = stream
        };
    }

    private static int ExtractLeadingInt(string value)
    {
        var match = LeadingDigits().Match(value);
        return match.Success ? int.Parse(match.Value) : 0;
    }

    public override string ToString()
    {
        return $"{Date:yyyyMMdd}.{Revision}";
    }

    public static bool operator >(OsuVersion? ver1, OsuVersion? ver2)
    {
        if (ver1 is null) return false;
        if (ver2 is null) return true;

        return ver1.Date > ver2.Date || ver1.Date == ver2.Date && ver1.Revision > ver2.Revision;
    }

    public static bool operator <(OsuVersion? ver1, OsuVersion? ver2)
    {
        return ver2 > ver1;
    }

    public static bool operator ==(OsuVersion? ver1, OsuVersion? ver2)
    {
        if (ver1 is null && ver2 is null) return true;
        if (ver1 is null || ver2 is null) return false;

        return ver1.Date == ver2.Date && ver1.Revision == ver2.Revision;
    }

    public static bool operator !=(OsuVersion? ver1, OsuVersion? ver2)
    {
        return !(ver1 == ver2);
    }

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingDigits();
}