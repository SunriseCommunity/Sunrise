using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchStart)]
public class MultiMatchStartHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var match = session.Match;

        if (match == null || match?.Match.HostId != session.User.Id)
            return Task.CompletedTask;

        match.StartGame();

        return Task.CompletedTask;
    }
}