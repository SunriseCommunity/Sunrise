using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects;

public class Session
{
    private readonly PacketHelper _helper;
    public readonly UserAttributes Attributes;

    public readonly string Token;

    public Session(User user, Location location, SunriseDb database)
    {
        _helper = new PacketHelper();

        User = user;
        Token = Guid.NewGuid().ToString();
        Attributes = new UserAttributes(User, location, database);
    }

    public User User { get; private set; }

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

    public void SendChannelMessage(string channel, string message, string? sender = null)
    {
        _helper.WritePacket(PacketType.ServerChatMessage,
            new BanchoChatMessage
            {
                Message = message,
                Sender = sender ?? Configuration.BotUsername,
                Channel = channel
            });
    }

    public void SendExistingChannels()
    {
        _helper.WritePacket(PacketType.ServerChatChannelListingComplete, 0);
    }

    public void WritePacket<T>(PacketType type, T data)
    {
        _helper.WritePacket(type, data);
    }


    public async void SendFriendsList()
    {
        await FetchUser();

        _helper.WritePacket(PacketType.ServerFriendsList, User.FriendsList);
    }

    public byte[] GetContent()
    {
        return _helper.GetBytesToSend();
    }

    public async Task FetchUser()
    {
        var user = await ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>().GetUser(User.Id);
        User = user ?? User;
    }
}