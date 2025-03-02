using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiSlotChange)]
public class MultiSlotChangeHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var slotId = new BanchoInt(packet.Data);

        session.Match?.MovePlayer(session, slotId.Value);

        return Task.CompletedTask;
    }
}