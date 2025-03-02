using HOPEless.Bancho;
using HOPEless.osu;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiBeatmapMissing)]
public class MultiBeatmapMissingHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.UpdatePlayerStatus(session, MultiSlotStatus.NoMap);

        return Task.CompletedTask;
    }
}