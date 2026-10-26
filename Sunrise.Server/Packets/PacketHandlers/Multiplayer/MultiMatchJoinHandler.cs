using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories.Multiplayer;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchJoin)]
public class MultiMatchJoinHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var joinData = new BanchoMultiplayerJoin(packet.Data);

        var multiplayerMatches = ServicesProviderHolder.GetRequiredService<MatchRepository>();

        // TryJoinMatch sends the SendMultiMatchJoinFail packet if the join fails, so we don't need to handle non-successful joins here
        multiplayerMatches.TryJoinMatch(session, joinData);

        return Task.CompletedTask;
    }
}