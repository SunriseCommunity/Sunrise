using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiSkipRequest)]
public class MultiSkipRequestHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.SetPlayerSkipped(session);

        return Task.CompletedTask;
    }
}