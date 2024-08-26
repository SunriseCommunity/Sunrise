using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiInvite)]
public class MultiInviteHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var invitee = new BanchoInt(packet.Data);

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();
        var inviteeSession = sessions.GetSession(invitee.Value);

        if (inviteeSession == null)
            return Task.CompletedTask;

        var match = session.Match;
        if (match != null)
            inviteeSession.SendMultiInvite(match.Match, session);

        return Task.CompletedTask;
    }
}