using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiTransferHost)]
public class MultiTransferHostHandler : IHandler
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