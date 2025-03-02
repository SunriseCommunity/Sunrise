using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Chat;

[PacketHandler(PacketType.ClientAway)]
public class AwayHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var message = new BanchoChatMessage(packet.Data);
        session.Attributes.AwayMessage = message?.Message?.Trim() is null or "" ? null : message.Message.Trim();
        return Task.CompletedTask;
    }
}