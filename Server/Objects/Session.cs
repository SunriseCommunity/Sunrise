using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Objects;

public class Session
{
    private readonly PacketHelper _helper;
    public readonly UserAttributes Attributes;

    public readonly string Token;
    public readonly User User;

    public Session(User user, Location location, SunriseDb database)
    {
        _helper = new PacketHelper();

        User = user;
        Token = Guid.NewGuid().ToString();
        Attributes = new UserAttributes(User, location, database);
    }

    public void SendLoginResponse(LoginResponses response)
    {
        if (response != LoginResponses.Success)
        {
            _helper.WritePacket(PacketType.ServerLoginReply, response);
        }

        _helper.WritePacket(PacketType.ServerLoginReply, User.Id);
    }

    public void SendBanchoMaintenance()
    {
        const string message = "Bancho is currently in maintenance mode. Please try again later.";

        _helper.WritePacket(PacketType.ServerLoginReply, (int)LoginResponses.ServerError);
        _helper.WritePacket(PacketType.ServerNotification, message);
    }

    public void SendJoinChannel(string channel)
    {
        _helper.WritePacket(PacketType.ServerChatChannelAvailableAutojoin, channel);
        _helper.WritePacket(PacketType.ServerChatChannelJoinSuccess, channel);
    }

    public void SendChannelAvailable(ChatChannel channel)
    {
        _helper.WritePacket(PacketType.ServerChatChannelAvailable,
            new BanchoChatChannel
            {
                Name = channel.Name,
                Topic = channel.Description,
                UserCount = (short)channel.UsersCount()
            });
    }

    public void SendProtocolVersion(int version = 19)
    {
        _helper.WritePacket(PacketType.ServerBanchoVersion, version);
    }

    public void SendPrivilege()
    {
        _helper.WritePacket(PacketType.ServerUserPermissions, User.Privilege);
    }

    public async Task SendUserPresence()
    {
        var userPresence = await Attributes.GetPlayerPresence();
        _helper.WritePacket(PacketType.ServerUserPresence, userPresence);
    }

    public async Task SendUserData()
    {
        var userData = await Attributes.GetPlayerData();
        _helper.WritePacket(PacketType.ServerUserData, userData);
    }

    public void SendNotification(string text)
    {
        _helper.WritePacket(PacketType.ServerNotification, text);
    }

    public void SendExistingChannels()
    {
        _helper.WritePacket(PacketType.ServerChatChannelListingComplete, 0);
    }

    public void WritePacket<T>(PacketType type, T data)
    {
        _helper.WritePacket(type, data);
    }


    public void SendFriendsList()
    {
        // TODO - Get friends from database upon implementation
        var friends = new List<int>
        {
            1,
            2,
            3,
            4,
            5
        };

        _helper.WritePacket(PacketType.ServerFriendsList, friends);
    }

    public byte[] GetContent()
    {
        return _helper.GetBytesToSend();
    }
}