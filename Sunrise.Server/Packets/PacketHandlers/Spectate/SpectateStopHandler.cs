using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers.Spectate;

[PacketHandler(PacketType.ClientSpectateStop)]
public class SpectateStopHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var chatChannels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();

        chatChannels.LeaveChannel($"#spectator_{session.Spectating?.UserId}", session, true);

        session.Spectating?.RemoveSpectator(session);
        session.Spectating = null;

        return Task.CompletedTask;
    }
}