using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Chat;

[PacketHandler(PacketType.ClientUserToggleBlockNonFriendPm)]
public class UserToggleBlockNonFriendPmHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var isToggle = new BanchoInt(packet.Data);
        session.Attributes.IgnoreNonFriendPm = isToggle.Value == 1;
        return Task.CompletedTask;
    }
}