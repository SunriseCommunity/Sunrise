using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiSettingsChange)]
public class MultiSettingsChangeHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var changes = new BanchoMultiplayerMatch(packet.Data);

        var matchRepository = ServicesProviderHolder.GetRequiredService<MatchRepository>();
        matchRepository.UpdateMatch(session, changes);

        return Task.CompletedTask;
    }
}