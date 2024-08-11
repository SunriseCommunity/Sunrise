using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientDisconnect)]
public class DisconnectHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        // Thanks osu! client for trying to disconnect us right after we connect :)
        if (session.Attributes.LastLogin > DateTime.UtcNow.AddSeconds(-2))
        {
            return Task.CompletedTask;
        }

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        sessions.WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
        sessions.RemoveSession(session.User.Id);
        return Task.CompletedTask;
    }
}