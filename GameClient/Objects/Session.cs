using HOPEless.Bancho;
using Sunrise.Database.Schemas;
using Sunrise.GameClient.Objects.Serializable;
using Sunrise.GameClient.Types.Enums;

namespace Sunrise.GameClient.Objects;

public class Session
{
    private readonly PacketWriter _writer;

    public readonly string Token;
    public readonly User User;
    public readonly UserAttributes Attributes;

    public Session(User user, Location location)
    {
        _writer = new PacketWriter();

        User = user;
        Token = Guid.NewGuid().ToString();
        Attributes = new UserAttributes(User, location);
    }

    public void SendLoginResponse(LoginResponses response)
    {
        if (response != LoginResponses.Success)
        {
            _writer.WritePacket(PacketType.ServerLoginReply, response);
        }

        _writer.WritePacket(PacketType.ServerLoginReply, User.Id);
    }

    public void SendBanchoMaintenance()
    {
        const string message = "Bancho is currently in maintenance mode. Please try again later.";

        _writer.WritePacket(PacketType.ServerLoginReply, (int)LoginResponses.ServerError);
        _writer.WritePacket(PacketType.ServerNotification, message);
    }

    public void SendProtocolVersion(int version = 19)
    {
        _writer.WritePacket(PacketType.ServerBanchoVersion, version);
    }

    public void SendPrivilege()
    {
        _writer.WritePacket(PacketType.ServerUserPermissions, User.Privilege);
    }


    public void SendUserPresence()
    {
        var userPresence = Attributes.GetPlayerPresence();
        _writer.WritePacket(PacketType.ServerUserPresence, userPresence);
    }

    public void SendUserData()
    {
        var userData = Attributes.GetPlayerData();
        _writer.WritePacket(PacketType.ServerUserData, userData);
    }


    public void SendNotification(string text)
    {
        _writer.WritePacket(PacketType.ServerNotification, text);
    }

    public void SendUserQuit()
    {
        _writer.WritePacket(PacketType.ServerUserQuit, User.Id);
    }

    public void SendExistingChannels()
    {
        _writer.WritePacket(PacketType.ServerChatChannelListingComplete, 0);
    }

    public void WritePacket(PacketType type, object data)
    {
        _writer.WritePacket(type, data);
    }

    public void SendFriendsList()
    {
        // TODO - Get friends from database upon implementation
        var friends = new List<int>() { 1, 2, 3, 4, 5 };

        _writer.WritePacket(PacketType.ServerFriendsList, friends);
    }

    public byte[] GetContent()
    {
        return _writer.GetBytesToSend();
    }
}

