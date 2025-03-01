using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientStatusRequestOwn)]
public class StatusRequestOwnHandler : IPacketHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        await session.SendUserData();
    }
}