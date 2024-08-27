using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiScoreUpdate)]
public class MultiScoreUpdateHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var score = new BanchoScoreFrame(packet.Data);

        session.Match?.SendPlayerScoreUpdate(session, score);

        return Task.CompletedTask;
    }
}