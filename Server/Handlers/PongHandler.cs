using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientPong, true)]
public class PongHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Attributes.UpdateLastPing();
        return Task.CompletedTask;
    }
}