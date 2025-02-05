using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientStatusRequestOwn)]
public class StatusRequestOwnHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        await session.SendUserData();
    }
}