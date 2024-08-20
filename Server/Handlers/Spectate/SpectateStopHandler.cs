using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Spectate;

[PacketHandler(PacketType.ClientSpectateStop)]
public class SpectateStopHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Spectating?.RemoveSpectator(session);
        session.Spectating = null;

        return Task.CompletedTask;
    }
}