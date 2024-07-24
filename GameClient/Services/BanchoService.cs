using System.ComponentModel;
using HOPEless.Bancho;
using HOPEless.osu;
using osu.Shared;
using osu.Shared.Serialization;
using Sunrise.Database;
using Sunrise.Types.Classes;
using Sunrise.Types.Enums;
using Sunrise.Types.Objects;

namespace Sunrise.Services;

[Description("This class is responsible for sending packets to the client.")]
public class BanchoService
{
    private readonly ServicesProvider _servicesProvider;
    private readonly PacketQueue _queue = new PacketQueue();
    public PlayerObject? PlayerObject { get; private set; }

    public BanchoService(ServicesProvider servicesProvider)
    {
        _servicesProvider = servicesProvider;
    }

    public void SetPlayer(UserSchema user)
    {
        var player = new PlayerObject(user);
        PlayerObject = player;
    }

    public void SendLoginResponse(LoginResponses response)
    {
        var isSuccessful = response == LoginResponses.Success;
        var dataInt = (isSuccessful && PlayerObject != null) ? PlayerObject.Player.Id : (int)response;

        var packet = new BanchoPacket(PacketType.ServerLoginReply, BitConverter.GetBytes(dataInt));

        _queue.EnqueuePacket(packet);
    }

    public void SetBanchoMaintenance()
    {
        const string message = "Bancho is currently in maintenance mode. Please try again later.";

        SendNotification(message);
        SendLoginResponse(LoginResponses.ServerError);
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

    public async void SendFriendsList()
    {

        await using var writer = new SerializationWriter(new MemoryStream());

        var friendsData = new List<object>();

        var friends = new List<int> { 1, 2, 3, 4 };

        friends.Remove(PlayerObject.Player.Id);

        writer.Write((short)friends.Count);

        foreach (var friend in friends)
        {
            writer.Write(friend);
        }

        var data = ((MemoryStream)writer.BaseStream).ToArray();
        var packet = new BanchoPacket(PacketType.ServerFriendsList, data);

        _queue.EnqueuePacket(packet);
    }

    public void SendUserData(Player? otherPlayer = null)
    {
        var player = otherPlayer ?? PlayerObject.Player;
        if (player == null)
        {
            Console.WriteLine("Player is null. Cannot send user data.");
            return;
        }

        using var writer = new SerializationWriter(new MemoryStream());

        writer.Write(player.Id);
        writer.Write(player.Username);
        writer.Write((byte)player.Timezone);
        writer.Write((byte)player.Country);
        writer.Write((byte)player.Privilege);
        writer.Write(player.Longitude);
        writer.Write(player.Latitude);
        writer.Write(player.Id);

        var packet = new BanchoPacket(PacketType.ServerUserPresence, ((MemoryStream)writer.BaseStream).ToArray());

        _queue.EnqueuePacket(packet);
    }

    // Dirty copy of SendUserData. Will remove later
    public BanchoPacket GetUserData()
    {
        var player = PlayerObject.Player;
        if (player == null)
        {
            Console.WriteLine("Player is null. Cannot send user data.");
            return null;
        }

        using var writer = new SerializationWriter(new MemoryStream());

        writer.Write(player.Id);
        writer.Write(player.Username);
        writer.Write((byte)player.Timezone);
        writer.Write((byte)player.Country);
        writer.Write((byte)player.Privilege);
        writer.Write(player.Longitude);
        writer.Write(player.Latitude);
        writer.Write(player.Id);

        var packet = new BanchoPacket(PacketType.ServerUserPresence, ((MemoryStream)writer.BaseStream).ToArray());

        return packet;
    }

    public void SendUserStats(Player? otherPlayer = null)
    {
        var player = otherPlayer ?? PlayerObject?.Player;

        using var writer = new SerializationWriter(new MemoryStream());

        if (player == null)
        {
            Console.WriteLine("Player is null. Cannot send user stats.");
            return;
        }

        // set funny player stats
        player.PlayerStatus.Action = (BanchoAction)3;
        player.PlayerStatus.ActionText = "Playing osu!";
        player.PlayerStatus.BeatmapChecksum = "1234567890";
        player.PlayerStatus.CurrentMods = Mods.DoubleTime;
        player.PlayerStatus.PlayMode = (GameMode)0;
        player.PlayerStatus.BeatmapId = 1;

        writer.Write(player.Id);
        writer.Write((byte)player.PlayerStatus.Action);
        writer.Write(player.PlayerStatus.ActionText);
        writer.Write(player.PlayerStatus.BeatmapChecksum);
        writer.Write((uint)player.PlayerStatus.CurrentMods);
        writer.Write((byte)player.PlayerStatus.PlayMode);
        writer.Write(player.PlayerStatus.BeatmapId);
        writer.Write(player.RankedScore + player.TotalScore);
        writer.Write(player.Accuracy);
        writer.Write(player.PlayCount);
        writer.Write(player.TotalScore);
        writer.Write(player.Id);
        writer.Write((short)player.PerformancePoints);

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

    //have no idea how to use it or where to use this method
    public void LockPlayer(int seconds)
    {
        using var writer = new SerializationWriter(new MemoryStream());
        writer.Write(seconds);

        var packet = new BanchoPacket(PacketType.ServerRtx, ((MemoryStream)writer.BaseStream).ToArray());
        _queue.EnqueuePacket(packet);
    }


    public void SendUserDataBundle(int except)
    {
        using var ms = new MemoryStream();
        using var sw = new SerializationWriter(ms);

        var playersId = new List<int>(_servicesProvider.Players.GetAllPlayers()
            .Where(x => x.Id != except)
            .Select(x => x.Id));

        sw.Write(playersId);

        var packet = new BanchoPacket(PacketType.ServerUserPresenceBundle,
            ((MemoryStream)sw.BaseStream).ToArray());

        _queue.EnqueuePacket(packet);
    }

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
        foreach (var player in _servicesProvider.Players.GetAllPlayers())
        {
            player.Queue.EnqueuePacket(packet);
        }
    }
}