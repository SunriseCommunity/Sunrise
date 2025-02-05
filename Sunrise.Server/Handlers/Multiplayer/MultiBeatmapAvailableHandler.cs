using HOPEless.Bancho;
using HOPEless.osu;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiBeatmapAvailable)]
public class MultiBeatmapAvailableHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.UpdatePlayerStatus(session, MultiSlotStatus.NotReady);

        return Task.CompletedTask;
    }
}