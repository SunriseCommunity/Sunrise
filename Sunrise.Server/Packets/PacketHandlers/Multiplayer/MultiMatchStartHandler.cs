using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Session;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiMatchStart)]
public class MultiMatchStartHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var match = session.Match;

        if (match == null || match?.Match.HostId != session.User.Id)
            return Task.CompletedTask;

        match.StartGame();

        return Task.CompletedTask;
    }
}