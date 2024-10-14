using HOPEless.Bancho;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientDisconnect)]
public class DisconnectHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        sessions.WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
        sessions.RemoveSession(session);

        return Task.CompletedTask;
    }
}