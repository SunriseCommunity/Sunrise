using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories.Multiplayer;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiSettingsChange)]
public class MultiSettingsChangeHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var changes = new BanchoMultiplayerMatch(packet.Data);

        var matchRepository = ServicesProviderHolder.GetRequiredService<MatchRepository>();
        matchRepository.UpdateMatch(session, changes);

        return Task.CompletedTask;
    }
}