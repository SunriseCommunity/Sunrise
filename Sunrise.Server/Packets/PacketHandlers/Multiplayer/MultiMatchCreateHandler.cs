using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories.Multiplayer;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchCreate)]
public class MultiMatchCreateHandler : IPacketHandler
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