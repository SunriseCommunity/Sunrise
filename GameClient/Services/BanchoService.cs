using HOPEless.Bancho;
using HOPEless.osu;
using osu.Shared;
using osu.Shared.Serialization;
using Sunrise.Enums;
using Sunrise.GameClient.Types.Objects;
using Sunrise.Types.Classes;
using Sunrise.Types.Objects;

namespace Sunrise.Services;

public class BanchoService
{
    private readonly PlayerRepository _playerRepository;

    public PlayerObject? PlayerObject = null;
    public Player? Player => PlayerObject?.Player;

    private PacketQueue _queue => PlayerObject.Player.Queue;

    public BanchoService(PlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
    }

    public void SetPlayer(PlayerObject player)
    {
        PlayerObject = player;
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
        var packet = new BanchoPacket(PacketType.ServerUserPermissions, BitConverter.GetBytes((int)PlayerObject.Player.Privilege));

        _queue.EnqueuePacket(packet);
    }

    public void SendUserData(Player? otherPlayer = null)
    {
        var player = otherPlayer ?? PlayerObject.Player;

        if (player == null)
        {
            return;
        }

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
        var player = otherPlayer ?? PlayerObject.Player;

        using var writer = new SerializationWriter(new MemoryStream());

        if (player == null)
        {
            return;
        }

        // set funny player stats
        player.PlayerStatus.Action = (BanchoAction)3;
        player.PlayerStatus.ActionText = "Playing osu!";
        player.PlayerStatus.BeatmapChecksum = "1234567890";
        player.PlayerStatus.CurrentMods = Mods.DoubleTime;
        player.PlayerStatus.PlayMode = (GameMode)1;
        player.PlayerStatus.BeatmapId = 1;

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

        Console.Write("send user with id: ", player.Id);

        var packet = new BanchoPacket(PacketType.ServerUserData, ((MemoryStream)writer.BaseStream).ToArray());

        _queue.EnqueuePacket(packet);
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

    //have no idea how to use it or where to use this method
    public void LockPlayer(int seconds)
    {
        using var writer = new SerializationWriter(new MemoryStream());
        writer.Write(seconds);

        var packet = new BanchoPacket(PacketType.ServerRtx, ((MemoryStream)writer.BaseStream).ToArray());
        _queue.EnqueuePacket(packet);
    }


    // public void SendUserDataBundle(int except)
    // {
    //     using var ms = new MemoryStream();
    //     using var sw = new SerializationWriter(ms);
    //
    //     var playersId = new List<int>(_playerRepository.GetAllPlayers()
    //         .Where(x => x.Id != except)
    //         .Select(x => x.Id));
    //
    //     sw.Write(playersId);
    //
    //     var packet = new BanchoPacket(PacketType.ServerUserPresenceBundle,
    //         ((MemoryStream)sw.BaseStream).ToArray());
    //     
    //     _queue.EnqueuePacket(packet);
    // }

    public void SendExistingChannels()
    {
        var packet = new BanchoPacket(PacketType.ServerChatChannelListingComplete, BitConverter.GetBytes(0));

        _queue.EnqueuePacket(packet);
    }

    public byte[] GetPacketBytes()
    {
        return _queue.GetBytesToSend();
    }

    public void EnqueuePacketForEveryone(BanchoPacket packet)
    {
        foreach (var player in _playerRepository.GetAllPlayers())
        {
            player.Queue.EnqueuePacket(packet);
        }
    }
}