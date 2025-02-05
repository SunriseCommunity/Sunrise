using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchJoin)]
public class MultiMatchJoinHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var joinData = new BanchoMultiplayerJoin(packet.Data);

        var multiplayerMatches = ServicesProviderHolder.GetRequiredService<MatchRepository>();

        multiplayerMatches.JoinMatch(session, joinData);

        return Task.CompletedTask;
    }
}