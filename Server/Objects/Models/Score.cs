using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories;
using Sunrise.Server.Utils;
using Watson.ORM.Core;

namespace Sunrise.Server.Objects.Models;

[Table("score")]
public class Score
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int UserId { get; set; }

    [Column(DataTypes.Int, false)]
    public int BeatmapId { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string BeatmapHash { get; set; }

    [Column(DataTypes.Int, false)]
    public int ReplayFileId { get; set; }

    [Column(DataTypes.Int, false)]
    public int TotalScore { get; set; }

    [Column(DataTypes.Int, false)]
    public int MaxCombo { get; set; }

    [Column(DataTypes.Int, false)]
    public int Count300 { get; set; }

    [Column(DataTypes.Int, false)]
    public int Count100 { get; set; }

    [Column(DataTypes.Int, false)]
    public int Count50 { get; set; }

    [Column(DataTypes.Int, false)]
    public int CountMiss { get; set; }

    [Column(DataTypes.Int, false)]
    public int CountKatu { get; set; }

    [Column(DataTypes.Int, false)]
    public int CountGeki { get; set; }

    [Column(DataTypes.Boolean, false)]
    public bool Perfect { get; set; }

    [Column(DataTypes.Int, false)]
    public Mods Mods { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Grade { get; set; }

    [Column(DataTypes.Boolean, false)]
    public bool IsPassed { get; set; }

    [Column(DataTypes.Int, false)]
    public GameMode GameMode { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime WhenPlayed { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string OsuVersion { get; set; }

    [Column(DataTypes.Decimal, 100, 2, false)]
    public double Accuracy { get; set; }

    [Column(DataTypes.Decimal, 100, 2, false)]
    public double PerformancePoints { get; set; }

    // Local properties
    public Beatmap Beatmap { get; set; }
    public string FileChecksum { get; set; }
    public int? LeaderboardRank { get; set; }

    public Score SetNewScoreFromString(string scoreString, Beatmap beatmap, string version)
    {
        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        var split = scoreString.Split(':');
        var session = sessions.GetSession(username: split[1].Trim());

        if (session == null)
            throw new Exception("Session not found for score submission");

        BeatmapHash = split[0];
        UserId = session.User.Id;
        BeatmapId = beatmap.Id;
        FileChecksum = split[2]; // TODO: Check file checksum
        Count300 = int.Parse(split[3]);
        Count100 = int.Parse(split[4]);
        Count50 = int.Parse(split[5]);
        CountGeki = int.Parse(split[6]);
        CountKatu = int.Parse(split[7]);
        CountMiss = int.Parse(split[8]);
        TotalScore = int.Parse(split[9]);
        MaxCombo = int.Parse(split[10]);
        Perfect = bool.Parse(split[11]);
        Grade = split[12];
        Mods = (Mods)int.Parse(split[13]);
        IsPassed = bool.Parse(split[14]);
        GameMode = (GameMode)int.Parse(split[15]);
        WhenPlayed = DateTime.UtcNow;
        OsuVersion = version;
        Accuracy = Calculators.CalculateAccuracy(this);
        PerformancePoints = Calculators.CalculatePerformancePoints(session, this);
        Beatmap = beatmap;
        return this;
    }

    public async Task<string> GetString()
    {
        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var time = (int)WhenPlayed.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        var username = (await database.GetUser(UserId))?.Username ?? "Unknown";
        var hasReplay = ReplayFileId != null ? "1" : "0";

        return $"{Id}|{username}|{TotalScore}|{MaxCombo}|{Count50}|{Count100}|{Count300}|{CountMiss}|{CountKatu}|{CountGeki}|{Perfect}|{(int)Mods}|{UserId}|{LeaderboardRank ?? 0}|{time}|{hasReplay}";
    }
}