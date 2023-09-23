using osu.Shared;
using Sunrise.Enums;
using Sunrise.Services;

namespace Sunrise.Objects;

public class Score
{
    private readonly PlayerRepository _repository;
    public int d { get; set; }
    public string BeatmapHash { get; set; }
    public string Username { get; set; }
    public int Count300 { get; set; }
    public int Count100 { get; set; }
    public int Count50 { get; set; }
    public int CountGeki { get; set; }
    public int CountKatu { get; set; }
    public int CountMiss { get; set; }
    public int Total { get; set; }
    public int MaxCombo { get; set; }
    public bool IsFullCombo { get; set; }
    public string Rank { get; set; }
    public string Mods { get; set; }
    public bool IsMapPassed { get; set; }
    public PlayModes PlayMode { get; set; }
    public string WhenPlayed { get; set; }
    public string OsuVersion { get; set; }

    public void ParseScore(string strToParse)
    {
        var splittedString = strToParse.Split(":");

        BeatmapHash = splittedString[0];
        Username = splittedString[1];
        Count300 = ushort.Parse(splittedString[3]);
        Count100 = ushort.Parse(splittedString[4]);
        Count50 = ushort.Parse(splittedString[5]);
        CountGeki = ushort.Parse(splittedString[6]);
        CountKatu = ushort.Parse(splittedString[7]);
        CountMiss = ushort.Parse(splittedString[8]);
        Total = ushort.Parse(splittedString[9]);
        MaxCombo = ushort.Parse(splittedString[10]);
        IsFullCombo = bool.Parse(splittedString[11]);
        Rank = splittedString[12];
        Mods = splittedString[13];
        IsMapPassed = bool.Parse(splittedString[14]);
        PlayMode = (PlayModes)int.Parse(splittedString[15]);
        WhenPlayed = splittedString[16];
        OsuVersion = splittedString[17];
    }

    public override string ToString()
    {
        return $"Username: {Username} " +
               $"BHash: {BeatmapHash} " +
               $"Count300: {Count300} " +
               $"Count100: {Count100} " +
               $"Count50: {Count50} " +
               $"CountGeki: {CountGeki} " +
               $"CountKatu: {CountKatu} " +
               $"CountMiss: {CountMiss} " +
               $"Total: {Total} " +
               $"MaxCombo: {MaxCombo} " +
               $"IsFullCombo: {IsFullCombo} " +
               $"Rank: {Rank} " +
               $"Mods: {Mods} " +
               $"IsMapPassed: {IsMapPassed} " +
               $"PlayMode: {PlayMode} " +
               $"WhenPlayed: {WhenPlayed} " +
               $"OsuVersion: {OsuVersion} ";
    }
}