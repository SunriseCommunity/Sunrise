namespace Sunrise.Server.Utils;

public static class Configuration
{
    public static bool IgnoreBeatmapRanking { get; set; } = true;
    public static string RedisConnection { get; set; } = "localhost:6379";
    public static string WelcomeMessage { get; set; } = "Welcome to Sunrise!";
    public static string Domain { get; set; } = "sunrise.local";
}