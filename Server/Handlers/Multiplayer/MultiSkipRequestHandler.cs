using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiSkipRequest)]
public class MultiSkipRequestHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.SetPlayerSkipped(session);

        return Task.CompletedTask;
    }
}