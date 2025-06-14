using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers.Spectate;

[PacketHandler(PacketType.ClientSpectateStart)]
public class SpectateStartHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var target = new BanchoInt(packet.Data);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var targetSession = sessions.GetSession(userId: target.Value);

        targetSession?.AddSpectator(session);

        var chatChannels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();
        chatChannels.JoinChannel($"#spectator_{targetSession?.UserId}", session, true);

        return Task.CompletedTask;
    }
}