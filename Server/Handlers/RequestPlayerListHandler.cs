using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientRequestPlayerList, true)]
public class RequestPlayerListHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        return Task.CompletedTask;
    }
}