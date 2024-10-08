using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientUserPresenceRequest)]
public class UserPresenceRequestHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        await session.SendUserPresence();
    }
}