using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

// Holly fucked up with naming, so this is actually a "client" packet.
[PacketHandler(PacketType.ServerMultiMatchCompleted)]
public class MultiMatchCompletedHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.SetPlayerCompleted(session);

        return Task.CompletedTask;
    }
}