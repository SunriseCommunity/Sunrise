using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using osu.Shared.Serialization;
using Sunrise.Enums;
using Sunrise.Objects;

namespace Sunrise.Services;

public class BanchoService
{
    private readonly PlayerRepository _playerRepository;
    private PacketQueue _queue = new PacketQueue();

    public Player Player;

    public BanchoService(PlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
    }
    
    public void SendLoginResponse(LoginResponses response)
    {
        var packet = new BanchoPacket(PacketType.ServerLoginReply, BitConverter.GetBytes((int)response));

        _queue.EnqueuePacket(packet);
    }

    public void SendProtocolVersion(int version = 19)
    {
        var packet = new BanchoPacket(PacketType.ServerBanchoVersion, BitConverter.GetBytes(version));

        _queue.EnqueuePacket(packet);
    }

    public void SendPrivilege()
    {
        var packet = new BanchoPacket(PacketType.ServerUserPermissions, BitConverter.GetBytes((int)Player.Privilege));

        _queue.EnqueuePacket(packet);
    }

    public void SendUserData(Player? otherPlayer = null)
    {
        var player = Player;

        if (otherPlayer != null)
            player = otherPlayer;
        
        using var writer = new SerializationWriter(new MemoryStream());

        writer.Write(player.Id);
        writer.Write(player.Username);
        writer.Write((byte)player.Timezone);
        writer.Write(player.Country);
        writer.Write((byte)player.Privilege);
        writer.Write(player.Longitude);
        writer.Write(player.Latitude);
        writer.Write(player.RankPosition);

        var packet = new BanchoPacket(PacketType.ServerUserPresence, ((MemoryStream)writer.BaseStream).ToArray());

        _queue.EnqueuePacket(packet);
    }

    public void SendUserStats(Player? otherPlayer = null)
    {
        var player = Player;

        if (otherPlayer != null)
            player = otherPlayer;
        
        using var writer = new SerializationWriter(new MemoryStream());
        
        writer.Write(player.Id);
        writer.Write((byte)player.PlayerStatus.Action);
        writer.Write(player.PlayerStatus.ActionText);
        writer.Write(player.PlayerStatus.BeatmapChecksum);
        writer.Write((uint)player.PlayerStatus.CurrentMods);
        writer.Write((byte)player.PlayerStatus.PlayMode);
        writer.Write(player.PlayerStatus.BeatmapId);
        writer.Write(player.RankedScore);
        writer.Write(player.Accuracy);
        writer.Write(player.PlayCount);
        writer.Write(player.TotalScore);
        writer.Write(player.RankPosition);
        writer.Write((short)player.PerformancePoints);
        
        var packet = new BanchoPacket(PacketType.ServerUserData, ((MemoryStream)writer.BaseStream).ToArray());

        _queue.EnqueuePacket(packet);
    }

    public void UpdateUserStatus(BanchoUserStatus status)
    {
        Player.PlayerStatus = status;
    }

    public void SendNotification(string notification)
    {
        using var writer = new SerializationWriter(new MemoryStream());
        writer.Write(notification);

        var packet = new BanchoPacket(PacketType.ServerNotification, ((MemoryStream)writer.BaseStream).ToArray());

        _queue.EnqueuePacket(packet);
    }

    public void SendUserDataSingle(int id)
    {
        var packet = new BanchoPacket(PacketType.ServerUserPresenceSingle, BitConverter.GetBytes(id));
        
        _queue.EnqueuePacket(packet);
    }
    
    public void SendUserDataBundle(int except)
    {
        using var ms = new MemoryStream();
        using var sw = new SerializationWriter(ms);

        var playersId = new List<int>(_playerRepository.GetAllPlayers()
            .Where(x => x.Id != except)
            .Select(x => x.Id));

        sw.Write(playersId);

        var packet = new BanchoPacket(PacketType.ServerUserPresenceBundle,
            ((MemoryStream)sw.BaseStream).ToArray());
        
        _queue.EnqueuePacket(packet);
    }
    
    public void ListingChannelComplete()
    {
        var packet = new BanchoPacket(PacketType.ServerChatChannelListingComplete, BitConverter.GetBytes(0));
        
        _queue.EnqueuePacket(packet);
    }
    
    public byte[] GetPacketBytes()
    {
        return _queue.GetBytesToSend();
    }
}