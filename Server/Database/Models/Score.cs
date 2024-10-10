using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories;
using Sunrise.Server.Utils;
using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models;

[Table("score")]
public class Score
{
    [Column(true, DataTypes.Int, false)] public int Id { get; set; }

    [Column(DataTypes.Int, false)] public int UserId { get; set; }

    [Column(DataTypes.Int, false)] public int BeatmapId { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string ScoreHash { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string BeatmapHash { get; set; }

    [Column(DataTypes.Int, false)] public int ReplayFileId { get; set; }

    [Column(DataTypes.Int, false)] public int TotalScore { get; set; }

    [Column(DataTypes.Int, false)] public int MaxCombo { get; set; }

    [Column(DataTypes.Int, false)] public int Count300 { get; set; }

    [Column(DataTypes.Int, false)] public int Count100 { get; set; }

    [Column(DataTypes.Int, false)] public int Count50 { get; set; }

    [Column(DataTypes.Int, false)] public int CountMiss { get; set; }

    [Column(DataTypes.Int, false)] public int CountKatu { get; set; }

    [Column(DataTypes.Int, false)] public int CountGeki { get; set; }

    [Column(DataTypes.Boolean, false)] public bool Perfect { get; set; }

    [Column(DataTypes.Int, false)] public Mods Mods { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Grade { get; set; }

    [Column(DataTypes.Boolean, false)] public bool IsPassed { get; set; }
    [Column(DataTypes.Boolean, false)] public bool IsRanked { get; set; }

    [Column(DataTypes.Int, false)] public GameMode GameMode { get; set; }

    [Column(DataTypes.DateTime, false)] public DateTime WhenPlayed { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string OsuVersion { get; set; }

    [Column(DataTypes.DateTime, false)] public DateTime ClientTime { get; set; }

    [Column(DataTypes.Decimal, 100, 2, false)]
    public double Accuracy { get; set; }

    [Column(DataTypes.Decimal, 100, 2, false)]
    public double PerformancePoints { get; set; }

    // TODO: Deprecate local properties.
    // Local properties
    public int? LeaderboardRank { get; set; }

    public async Task<int> GetLeaderboardRank()
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        return await database.GetLeaderboardRank(this);
    }

    public Score SetNewScoreFromString(string scoreString, Beatmap beatmap)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        var split = scoreString.Split(':');
        var session = sessions.GetSession(split[1].Trim());

        if (session == null)
            throw new Exception("Session not found for score submission");

        BeatmapHash = split[0];
        UserId = session.User.Id;
        BeatmapId = beatmap.Id;
        ScoreHash = split[2];
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
        IsRanked = beatmap.IsScoreable;
        GameMode = (GameMode)int.Parse(split[15]);
        WhenPlayed = DateTime.UtcNow;
        OsuVersion = split[17];
        ClientTime = DateTime.ParseExact(split[16], "yyMMddHHmmss", null);
        Accuracy = Calculators.CalculateAccuracy(this);
        PerformancePoints = Calculators.CalculatePerformancePoints(session, this);
        return this;
    }

    public async Task<string> GetString()
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var time = (int)WhenPlayed.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        var username = (await database.GetUser(UserId))?.Username ?? "Unknown";

        const string hasReplay = "1"; // We don't store score without replays

        return
            $"{Id}|{username}|{TotalScore}|{MaxCombo}|{Count50}|{Count100}|{Count300}|{CountMiss}|{CountKatu}|{CountGeki}|{Perfect}|{(int)Mods}|{UserId}|{LeaderboardRank ?? 0}|{time}|{hasReplay}";
    }

    public string ComputeOnlineHash(string username, string clientHash, string? storyboardHash)
    {
        return string.Format(
            "chickenmcnuggets{0}o15{1}{2}smustard{3}{4}uu{5}{6}{7}{8}{9}{10}{11}Q{12}{13}{15}{14:yyMMddHHmmss}{16}{17}",
            Count300 + Count100,
            Count50,
            CountGeki,
            CountKatu,
            CountMiss,
            BeatmapHash,
            MaxCombo,
            Perfect,
            username,
            TotalScore,
            Grade,
            (int)Mods,
            IsPassed,
            (int)GameMode,
            ClientTime,
            OsuVersion,
            clientHash,
            storyboardHash).CreateMD5();
    }
}