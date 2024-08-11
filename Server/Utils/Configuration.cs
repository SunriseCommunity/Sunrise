namespace Sunrise.Server.Utils;

public static class Configuration
{
    public static bool IgnoreBeatmapRanking { get; set; } = true;
    public static string RedisConnection { get; set; } = "localhost:6379";
    public static string WelcomeMessage { get; set; } = "Welcome to Sunrise!";
    public static string BotUsername { get; set; } = "Sunshine Bot";
    public static string BotPrefix { get; set; } = "!";
    public static string Domain { get; set; } = "sunrise.local";
    public static bool OnMaintenance { get; set; } = false;
}