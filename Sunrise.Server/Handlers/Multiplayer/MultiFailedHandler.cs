using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiFailed)]
public class MultiFailedHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.SendPlayerFailed(session);

        return Task.CompletedTask;
    }
}