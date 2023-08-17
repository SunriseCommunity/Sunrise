using Sunrise.Enums;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared.Serialization;

namespace Sunrise.Objects;

public class Player
{
    public int Id;
    public string Token;
    public string Username;
    public ushort Country;
    public short Timezone;
    public UserPrivileges Privilege;
    public float Accuracy;
    public long TotalScore;
    public int PlayCount;
    public long RankedScore;
    public int RankPosition;
    public int PerformancePoints;
    public double Latitude;
    public double Longitude;
    public PlayModes PlayMode;
    public BanchoUserStatus PlayerStatus = new();
    private PacketQueue _queue = new();

    public Player(int id, string username, ushort country,
        short timezone, UserPrivileges privilege)
    {
        Id = id;
        Username = username;
        Country = country;
        Timezone = timezone;
        Privilege = privilege;
    }
    
    public byte[] GetPacketBytes()
    {
        return _queue.GetBytesToSend();
    }
}