using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchLeave)]
public class MultiMatchLeaveHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        session.Match?.RemovePlayer(session);

        return Task.CompletedTask;
    }
}