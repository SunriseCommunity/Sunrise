using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiChangePassword)]
public class MultiChangePasswordHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var newMatch = new BanchoMultiplayerMatch(packet.Data);

        if (session.Match == null || session.Match.HasHostPrivileges(session) == false)
            return Task.CompletedTask;

        session.Match.ChangePassword(newMatch.GamePassword);

        return Task.CompletedTask;
    }
}