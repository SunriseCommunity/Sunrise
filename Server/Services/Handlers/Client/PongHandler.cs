using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services.Handlers.Client;

public class PongHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        session.Attributes.LastPingRequest = DateTime.UtcNow;
        return Task.CompletedTask;
    }
}