using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchLoadComplete)]
public class MultiMatchLoadCompleteHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.SetPlayerLoaded(session);

        return Task.CompletedTask;
    }
}