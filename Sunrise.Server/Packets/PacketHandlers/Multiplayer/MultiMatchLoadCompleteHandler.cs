using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchLoadComplete)]
public class MultiMatchLoadCompleteHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.SetPlayerLoaded(session);

        return Task.CompletedTask;
    }
}