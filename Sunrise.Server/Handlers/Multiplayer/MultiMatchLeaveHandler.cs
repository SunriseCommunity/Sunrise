using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchLeave)]
public class MultiMatchLeaveHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.RemovePlayer(session);

        return Task.CompletedTask;
    }
}