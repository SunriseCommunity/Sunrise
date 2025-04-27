using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Chat;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiInvite)]
public class MultiInviteHandler : IPacketHandler
{
    private readonly ChatRateLimiter _rateLimiter = new(6, TimeSpan.FromSeconds(4));

    public async Task Handle(BanchoPacket packet, Session session)
    {
        var invitee = new BanchoInt(packet.Data);

        if (!_rateLimiter.CanSend(session))
            return;

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var inviteeSession = sessions.GetSession(userId: invitee.Value);

        if (inviteeSession == null)
            return;

        if (inviteeSession.Attributes.IsBot)
            session.SendChannelMessage(Configuration.BotUsername,
                "Thanks for the invite, but if I join, who will moderate the chat?");

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var selfUser = await database.Users.GetUser(session.UserId);

        if (selfUser == null)
            return;

        var match = session.Match;
        if (match != null)
            inviteeSession.SendMultiInvite(match.Match, selfUser);
    }
}