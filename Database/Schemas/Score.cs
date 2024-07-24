using Sunrise.Types.Enums;
using Watson.ORM.Core;

namespace Sunrise.Database;

[Table("score")]
public class ScoreSchema
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int UserId { get; set; }

    [Column(DataTypes.Int, false)]
    public int BeatmapId { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string BeatmapHash { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string ReplayChecksum { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string FileChecksum { get; set; }

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
    public int Mods { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Grade { get; set; }

    [Column(DataTypes.Boolean, false)]
    public bool IsPassed { get; set; }

    [Column(DataTypes.Int, false)]
    public PlayModes PlayMode { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime WhenPlayed { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string OsuVersion { get; set; }

    [Column(DataTypes.Int, false)]
    public int Accuracy { get; set; }

    public ScoreSchema()
    {
    }

    public async Task<ScoreSchema> SetScoreFromString(string scoreString, ServicesProvider services)
    {
        var split = scoreString.Split(':');
        var user = await services.Database.GetUser(username: split[1].Trim());

        if (user == null)
        {
            Console.WriteLine("User not found");
            return null;
        }

        BeatmapHash = split[0];
        UserId = user.Id;
        ReplayChecksum = split[2];
        FileChecksum = split[2]; // Placeholder
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
        Mods = int.Parse(split[13]);
        IsPassed = bool.Parse(split[14]);
        PlayMode = (PlayModes)int.Parse(split[15]);
        WhenPlayed = DateTime.UtcNow;
        OsuVersion = split[17];
        Accuracy = 0; // TODO: CALCULATE 

        return this;
    }

    public async Task<string> GetString(int rank, ServicesProvider services)
    {
        var time = (int)WhenPlayed.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        var username = (await services.Database.GetUser(UserId)).Username;

        var has_replay = "0"; // TODO: Check if replay exists
        return $"{UserId}|{username}|{TotalScore}|{MaxCombo}|{Count50}|{Count100}|{Count300}|{CountMiss}|{CountKatu}|{CountGeki}|{Perfect}|{Mods}|{UserId}|{rank}|{time}|{has_replay}";
    }

}