using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchCreate)]
public class MultiMatchCreateHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var match = new BanchoMultiplayerMatch(packet.Data);

        var matchHistoryPublic =
            string.IsNullOrEmpty(match.GamePassword) || !match.GamePassword.EndsWith("//private");

        if (!matchHistoryPublic) match.GamePassword = match.GamePassword[..^9];

        if (string.IsNullOrEmpty(match.GamePassword))
            match.GamePassword = null;

        match.GamePassword = match.GamePassword?.Replace(" ", "_");

        var multiplayerMatches = ServicesProviderHolder.GetRequiredService<MatchRepository>();

        multiplayerMatches.CreateMatch(match);

        multiplayerMatches.JoinMatch(session,
            new BanchoMultiplayerJoin
            {
                MatchId = match.MatchId,
                Password = match.GamePassword
            });

        return Task.CompletedTask;
    }
}