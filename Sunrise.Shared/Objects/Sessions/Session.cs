using System.Collections.Concurrent;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Objects.Multiplayer;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Shared.Objects.Sessions;

public class Session : BaseSession
{
    private readonly PacketHelper _helper;
    public readonly UserAttributes Attributes;

    public readonly ConcurrentDictionary<int, Session> Spectators = [];

    public readonly string Token;

    public Session(User user, Location location, LoginRequest loginRequest) : base(user)
    {
        _helper = new PacketHelper();

        Token = Guid.NewGuid().ToString();
        Attributes = new UserAttributes(user, location, loginRequest);
    }

    // Note: Not the best place, but I'm out of ideas.
    public int? LastBeatmapIdUsedWithCommand { get; set; }

    public MultiplayerMatch? Match { get; set; } = null;

    public Session? Spectating { get; set; }

    public void SendLoginResponse(LoginResponse response)
    {
        if (response != LoginResponse.Success) _helper.WritePacket(PacketType.ServerLoginReply, response);

        _helper.WritePacket(PacketType.ServerLoginReply, UserId);
    }

    public void SendBanchoMaintenance()
    {
        var message = "Server going down for maintenance.";

        var user = GetSessionUser();
        if (user == null)
            return;

        if (user.Privilege.HasFlag(UserPrivilege.Admin)) return;

        message += " You will be disconnected shortly.";

        _helper.WritePacket(PacketType.ServerLoginReply, (int)LoginResponse.ServerError);
        _helper.WritePacket(PacketType.ServerNotification, message);
    }

    public void SendRestriction(string reason = "No reason provided.")
    {
        var message =
            $"You have been restricted. Reason: {reason}. Please contact a staff member for more information.";

        _helper.WritePacket(PacketType.ServerLoginReply, (int)LoginResponse.InvalidCredentials);
        _helper.WritePacket(PacketType.ServerNotification, message);
    }

    public void SendRateLimitWarning()
    {
        var message =
            "Whoa there! You're sending requests a little too fast. Because of this, some data like the leaderboard might not update correctly or could appear broken. Please slow down a bit!";

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
        var user = GetSessionUser();
        if (user == null)
            return;

        _helper.WritePacket(PacketType.ServerUserPermissions, user.GetPrivilegeRank() | PlayerRank.Supporter);
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
        _helper.WritePacket(PacketType.ServerSpectateNoBeatmap, UserId);
    }

    public void SendChannelMessage(string channel, string message, User? senderUser = null)
    {
        _helper.WritePacket(PacketType.ServerChatMessage,
            new BanchoChatMessage
            {
                Message = message,
                Sender = senderUser?.Username ?? Configuration.BotUsername,
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

    public void SendFriendsList(List<int> friendsIds)
    {
        _helper.WritePacket(PacketType.ServerFriendsList, friendsIds);
    }

    public void SendMultiMatchJoinFail()
    {
        _helper.WritePacket(PacketType.ServerMultiMatchJoinFail, -1);
    }

    public void SendMultiMatchJoinSuccess(BanchoMultiplayerMatch match)
    {
        _helper.WritePacket(PacketType.ServerMultiMatchJoinSuccess, match);
    }

    public void SendMultiInvite(BanchoMultiplayerMatch match, User sender)
    {
        var message = new BanchoChatMessage
        {
            Sender = sender.Username,
            SenderId = sender.Id,
            Channel = sender.Username,
            Message = $"Come join my multiplayer match! [osump://{match.MatchId}/{match.GamePassword} {match.GameName}]"
        };

        _helper.WritePacket(PacketType.ServerMultiInvite, message);
    }

    public byte[] GetContent()
    {
        return _helper.GetBytesToSend();
    }

    public void AddSpectator(Session session)
    {
        session.Spectating?.RemoveSpectator(session);

        foreach (var (_, spectator) in Spectators)
        {
            spectator.WritePacket(PacketType.ServerSpectateOtherSpectatorJoined, session.UserId);
            session.WritePacket(PacketType.ServerSpectateOtherSpectatorJoined, spectator.UserId);
        }

        // TODO: Add own session in spectator chat

        _helper.WritePacket(PacketType.ServerSpectateSpectatorJoined, session.UserId);
        session.Spectating = this;

        Spectators.TryAdd(session.UserId, session);
    }

    public void RemoveSpectator(Session session)
    {
        foreach (var (_, spectator) in Spectators)
        {
            spectator.WritePacket(PacketType.ServerSpectateOtherSpectatorLeft, session.UserId);
        }

        _helper.WritePacket(PacketType.ServerSpectateSpectatorLeft, session.UserId);
        session.Spectating = null;

        // TODO: Remove own session from spectator chat if no spectators left

        Spectators.TryRemove(session.UserId, out _);
    }

    private User? GetSessionUser()
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = database.Users.GetUser(UserId, options: new QueryOptions(true)).ConfigureAwait(false).GetAwaiter().GetResult();

        if (user == null)
            throw new ApplicationException($"User with id {UserId} not found");

        return user;
    }
}