using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiTransferHost)]
public class MultiTransferHostHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var slotId = new BanchoInt(packet.Data);

        if (session.Match == null || session.Match.HasHostPrivileges(session) == false)
            return Task.CompletedTask;

        session.Match.TransferHost(slotId.Value);

        return Task.CompletedTask;
    }
}