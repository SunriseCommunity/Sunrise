using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientPong, true)]
public class PongHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Attributes.UpdateLastPing();
        return Task.CompletedTask;
    }
}