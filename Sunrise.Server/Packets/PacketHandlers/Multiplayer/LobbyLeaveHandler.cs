using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories.Multiplayer;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientLobbyLeave)]
public class LobbyLeaveHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var matchRepository = ServicesProviderHolder.GetRequiredService<MatchRepository>();

        matchRepository.LeaveLobby(session);

        return Task.CompletedTask;
    }
}