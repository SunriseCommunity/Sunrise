using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiScoreUpdate)]
public class MultiScoreUpdateHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var score = new BanchoScoreFrame(packet.Data);

        session.Match?.SendPlayerScoreUpdate(session, score);

        return Task.CompletedTask;
    }
}