using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

// Holly fucked up with naming, so this is actually a "client" packet.
[PacketHandler(PacketType.ServerMultiMatchCompleted)]
public class MultiMatchCompletedHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.SetPlayerCompleted(session);

        return Task.CompletedTask;
    }
}