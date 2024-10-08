using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiInvite)]
public class MultiInviteHandler : IHandler
{
    private readonly RateLimiter _rateLimiter = new(6, TimeSpan.FromSeconds(4));

    public Task Handle(BanchoPacket packet, Session session)
    {
        var invitee = new BanchoInt(packet.Data);

        if (!_rateLimiter.CanSend(session)) return Task.CompletedTask;

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var inviteeSession = sessions.GetSession(userId: invitee.Value);

        if (inviteeSession == null)
            return Task.CompletedTask;

        if (inviteeSession.User.Username == Configuration.BotUsername)
            session.SendChannelMessage(Configuration.BotUsername,
                "Thanks for the invite, but if I join, who will moderate the chat?");

        var match = session.Match;
        if (match != null)
            inviteeSession.SendMultiInvite(match.Match, session);

        return Task.CompletedTask;
    }
}