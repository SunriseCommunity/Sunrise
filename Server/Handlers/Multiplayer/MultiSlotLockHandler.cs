using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiSlotLock)]
public class MultiSlotLockHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var slotId = new BanchoInt(packet.Data);

        session.Match?.UpdateLock(slotId.Value);

        return Task.CompletedTask;
    }
}