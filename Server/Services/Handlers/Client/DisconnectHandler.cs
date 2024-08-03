using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services.Handlers.Client;

public class DisconnectHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        // Thanks osu! client for trying to disconnect us right after we connect :)
        if (session.Attributes.LastLogin > DateTime.UtcNow.AddSeconds(-3))
        {
            return Task.CompletedTask;
        }

        services.Sessions.WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
        services.Sessions.RemoveSession(session.User.Id);
        return Task.CompletedTask;
    }
}