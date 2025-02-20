using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Types.Enums;

namespace Sunrise.Shared.Types.Interfaces;

public interface ISession : IBaseSession
{
    User User { get; }
    IUserAttributes Attributes { get; }

    int? LastBeatmapIdUsedWithCommand { get; set; }
    IMultiplayerMatch? Match { get; set; }
    ISession? Spectating { get; set; }
    List<ISession> Spectators { get; }

    void SendLoginResponse(LoginResponses response);

    void SendBanchoMaintenance();

    void SendRestriction(string reason = "No reason provided.");

    void SendJoinChannel(IChatChannel channel);

    void SendChannelAvailable(IChatChannel channel);

    void SendProtocolVersion(int version = 19);

    void SendPrivilege();

    void SendSilenceStatus(int time = 0, string? reason = null);

    Task SendUserPresence();

    Task SendUserData();

    void SendNotification(string text);

    void SendSpectatorMapless(ISession session);

    void SendChannelMessage(string channel, string message, string? sender = null);

    void SendExistingChannels();

    void WritePacket<T>(PacketType type, T data);

    void SendFriendsList();

    void SendMultiMatchJoinFail();

    void SendMultiMatchJoinSuccess(BanchoMultiplayerMatch match);

    void SendMultiInvite(BanchoMultiplayerMatch match, ISession sender);

    byte[] GetContent();

    Task UpdateUser(User? user = null);

    void AddSpectator(ISession session);

    void RemoveSpectator(ISession session);
}