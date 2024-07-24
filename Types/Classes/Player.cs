using HOPEless.Bancho.Objects;
using Sunrise.Database;
using Sunrise.Types.Enums;
using Sunrise.Types.Objects;

namespace Sunrise.Types.Classes;

public class Player
{
    public Player(UserSchema userSchema)
    {
        Id = userSchema.Id;
        Username = userSchema.Username;
        HashedPassword = userSchema.Passhash;
        Country = userSchema.Country;
        Privilege = userSchema.Privilege;
        Token = userSchema.Token;
        Accuracy = userSchema.Accuracy;
        TotalScore = userSchema.TotalScore;
        PlayCount = userSchema.PlayCount;
        RankedScore = userSchema.RankedScore;
        PerformancePoints = userSchema.PerformancePoints;
        PlayerStatus = new BanchoUserStatus();
        Queue = new PacketQueue();
    }

    public int Id { get; set; }
    public string Token { get; set; }
    public string Username { get; set; }
    public string HashedPassword { get; set; }
    public short Country { get; set; }
    public short Timezone { get; set; }
    public UserPrivileges Privilege { get; set; }
    public float Accuracy { get; set; }
    public long TotalScore { get; set; }
    public int PlayCount { get; set; }
    public long RankedScore { get; set; }
    public int RankPosition { get; set; }
    public int PerformancePoints { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public PlayModes PlayMode { get; set; }
    public BanchoUserStatus PlayerStatus = new();
    public PacketQueue Queue = new();

    private void GenerateRandomStats()
    {
        var random = new Random();
        TotalScore = random.Next(0, 1000000);
        PlayCount = random.Next(0, 1000);
        RankedScore = random.Next(0, 1000000);
        RankPosition = random.Next(0, 1000);
        PerformancePoints = random.Next(0, 1000);
        Accuracy = (float)random.NextDouble();
    }


}