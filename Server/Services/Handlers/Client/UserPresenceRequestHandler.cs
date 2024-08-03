using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services.Handlers.Client;

public class UserPresenceRequestHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        await session.SendUserPresence();
    }
}

