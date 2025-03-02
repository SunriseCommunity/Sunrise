using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiSlotLock)]
public class MultiSlotLockHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var slotId = new BanchoInt(packet.Data);

        session.Match?.UpdateLock(slotId.Value);

        return Task.CompletedTask;
    }
}