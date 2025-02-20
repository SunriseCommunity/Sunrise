using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Session;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientUserPresenceRequest)]
public class UserPresenceRequestHandler : IPacketHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        await session.SendUserPresence();
    }
}