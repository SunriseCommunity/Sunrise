using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchStart)]
public class MultiMatchStartHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var match = session.Match;

        if (match == null || match?.Match.HostId != session.UserId)
            return Task.CompletedTask;

        match.StartGame();

        return Task.CompletedTask;
    }
}