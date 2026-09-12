using System.Globalization;

namespace Sunrise.Shared.Utils;

public static class ScoreSubmissionVersionValidator
{
    public static bool IsValid(string? value)
    {
        return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
