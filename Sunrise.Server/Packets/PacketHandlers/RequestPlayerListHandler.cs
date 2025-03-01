using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientRequestPlayerList, true)]
public class RequestPlayerListHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        return Task.CompletedTask;
    }
}