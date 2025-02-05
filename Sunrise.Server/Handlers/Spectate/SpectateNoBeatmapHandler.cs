using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Spectate;

[PacketHandler(PacketType.ClientSpectateNoBeatmap)]
public class SpectateNoBeatmapHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Spectating?.SendSpectatorMapless(session);

        return Task.CompletedTask;
    }
}