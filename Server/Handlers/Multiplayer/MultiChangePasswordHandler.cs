using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiChangePassword)]
public class MultiChangePasswordHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var newMatch = new BanchoMultiplayerMatch(packet.Data);

        session.Match?.ChangePassword(session, newMatch.GamePassword);

        return Task.CompletedTask;
    }
}