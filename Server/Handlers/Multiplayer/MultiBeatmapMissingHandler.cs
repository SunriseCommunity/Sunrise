using HOPEless.Bancho;
using HOPEless.osu;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiBeatmapMissing)]
public class MultiBeatmapMissingHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.UpdatePlayerStatus(session, status: MultiSlotStatus.NoMap);

        return Task.CompletedTask;
    }
}