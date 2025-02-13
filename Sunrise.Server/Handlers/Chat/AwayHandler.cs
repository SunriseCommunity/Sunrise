using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Chat;

[PacketHandler(PacketType.ClientAway)]
public class AwayHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var message = new BanchoChatMessage(packet.Data);
        session.Attributes.AwayMessage = message?.Message?.Trim() is null or "" ? null : message.Message.Trim();
        return Task.CompletedTask;
    }
}