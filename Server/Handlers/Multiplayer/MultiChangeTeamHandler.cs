using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiChangeTeam)]
public class MultiChangeTeamHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.ChangeTeam(session);

        return Task.CompletedTask;
    }
}