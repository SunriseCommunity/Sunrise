using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Spectate;

[PacketHandler(PacketType.ClientSpectateNoBeatmap)]
public class SpectateNoBeatmapHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Spectating?.SendSpectatorMapless(session);

        return Task.CompletedTask;
    }
}