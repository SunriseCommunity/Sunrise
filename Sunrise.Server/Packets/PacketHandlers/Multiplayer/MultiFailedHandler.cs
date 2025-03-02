using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiFailed)]
public class MultiFailedHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.SendPlayerFailed(session);

        return Task.CompletedTask;
    }
}