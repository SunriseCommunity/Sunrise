using Sunrise.Enums;

public class Score
{
    public int d { get; set; }
    public string BeatmapHash { get; set; }
    public string Username { get; set; }
    public string OnlineChecksum { get; set; }
    public int Count300 { get; set; }
    public int Count100 { get; set; }
    public int Count50 { get; set; }
    public int CountGeki { get; set; }
    public int CountKatu { get; set; }
    public int CountMiss { get; set; }
    public int TotalScore { get; set; }
    public int MaxCombo { get; set; }
    public bool IsFullCombo { get; set; }
    public string Rank { get; set; }
    public string Mods { get; set; }
    public bool IsMapPassed { get; set; }
    public PlayModes PlayMode { get; set; }
    public DateTime WhenPlayed { get; set; }
    public string OsuVersion { get; set; }

}