using HOPEless.Bancho;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientLobbyJoin)]
public class LobbyJoinHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var matchRepository = ServicesProviderHolder.GetRequiredService<MatchRepository>();

        matchRepository.JoinLobby(session);

        return Task.CompletedTask;
    }
}