using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Chat;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects.Multiplayer;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Objects;

public class Session : BaseSession
{
    private readonly PacketHelper _helper;
    public readonly UserAttributes Attributes;

    public readonly List<Session> Spectators = [];

    public readonly string Token;

    public Session(User user, Location location, LoginRequest loginRequest) : base(user)
    {
        _helper = new PacketHelper();

        User = user;
        Token = Guid.NewGuid().ToString();
        Attributes = new UserAttributes(User, location, loginRequest);
    }

    // Note: Not the best place, but I'm out of ideas.
    public int? LastBeatmapIdUsedWithCommand { get; set; }

    public MultiplayerMatch? Match { get; set; } = null;

    public Session? Spectating { get; set; } = null;

    public User User { get; private set; }

    public void SendLoginResponse(LoginResponses response)
    {
        if (response != LoginResponses.Success) _helper.WritePacket(PacketType.ServerLoginReply, response);

        _helper.WritePacket(PacketType.ServerLoginReply, User.Id);
    }

    public void SendBanchoMaintenance()
    {
        var message = "Server going down for maintenance.";

        if (User.Privilege.HasFlag(UserPrivileges.Admin)) return;

        message += " You will be disconnected shortly.";

        _helper.WritePacket(PacketType.ServerLoginReply, (int)LoginResponses.ServerError);
        _helper.WritePacket(PacketType.ServerNotification, message);
    }

    public void SendRestriction(string reason = "No reason provided.")
    {
        var message =
            $"You have been restricted. Reason: {reason}. Please contact a staff member for more information.";

        _helper.WritePacket(PacketType.ServerLoginReply, (int)LoginResponses.InvalidCredentials);
        _helper.WritePacket(PacketType.ServerNotification, message);
    }

    public void SendNewLogin()
    {
        var message =
            "You have been logged in from another location. Please try again later.";

        _helper.WritePacket(PacketType.ServerLoginReply, (int)LoginResponses.InvalidCredentials);
        _helper.WritePacket(PacketType.ServerNotification, message);
    }

    public void SendJoinChannel(ChatChannel channel)
    {
        _helper.WritePacket(PacketType.ServerChatChannelJoinSuccess, channel.Name);
        SendChannelAvailable(channel);
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
        _helper.WritePacket(PacketType.ServerUserPermissions, User.GetPrivilegeRank() | PlayerRank.Supporter);
    }

    public void SendSilenceStatus(int time = 0, string? reason = null)
    {
        _helper.WritePacket(PacketType.ServerNotification,
            $"You have been {(time == 0 ? "un" : "")}silenced. {(reason != null ? $"Reason: {reason}" : "")}");
        _helper.WritePacket(PacketType.ServerLockClient, time);
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

    public void SendSpectatorMapless(Session session)
    {
        _helper.WritePacket(PacketType.ServerSpectateNoBeatmap, session.User.Id);
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
        _helper.WritePacket(PacketType.ServerFriendsList, User.FriendsList);
    }

    public void SendMultiMatchJoinFail()
    {
        _helper.WritePacket(PacketType.ServerMultiMatchJoinFail, -1);
    }

    public void SendMultiMatchJoinSuccess(BanchoMultiplayerMatch match)
    {
        _helper.WritePacket(PacketType.ServerMultiMatchJoinSuccess, match);
    }

    public void SendMultiInvite(BanchoMultiplayerMatch match, Session sender)
    {
        var message = new BanchoChatMessage
        {
            Sender = sender.User.Username,
            SenderId = sender.User.Id,
            Channel = sender.User.Username,
            Message = $"Come join my multiplayer match! [osump://{match.MatchId}/{match.GamePassword} {match.GameName}]"
        };

        _helper.WritePacket(PacketType.ServerMultiInvite, message);
    }


    public byte[] GetContent()
    {
        return _helper.GetBytesToSend();
    }

    public async Task UpdateUser(User? user = null)
    {
        if (user == null)
        {
            var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
            user = await database.GetUser(User.Id);
        }

        if (User.Id != user?.Id) throw new InvalidOperationException("Cannot update user with different ID.");

        User = user;
    }

    public void AddSpectator(Session session)
    {
        foreach (var spectator in Spectators)
            spectator.WritePacket(PacketType.ServerSpectateOtherSpectatorJoined, session.User.Id);

        _helper.WritePacket(PacketType.ServerSpectateSpectatorJoined, session.User.Id);

        Spectators.Add(session);
    }

    public void RemoveSpectator(Session session)
    {
        foreach (var spectator in Spectators)
            spectator.WritePacket(PacketType.ServerSpectateOtherSpectatorLeft, session.User.Id);

        _helper.WritePacket(PacketType.ServerSpectateSpectatorLeft, session.User.Id);

        Spectators.Remove(session);
    }
}