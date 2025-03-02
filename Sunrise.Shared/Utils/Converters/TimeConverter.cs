namespace Sunrise.Shared.Utils.Converters;

public static class TimeConverter
{
    public static string SecondsToString(int seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.ToString(@"mm\:ss");
    }

    public static string SecondsToMinutes(int seconds, bool showSeconds = false)
    {
        return seconds < 60 ? $"{seconds} second(s)" : $"{seconds / 60} minute(s) {(showSeconds ? $"{seconds % 60} second(s)" : "")}";
    }

    public static int ToSeconds(this TimeSpan time)
    {
        return (int)time.TotalSeconds;
    }
}