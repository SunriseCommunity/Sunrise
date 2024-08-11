using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Chat;

[PacketHandler(PacketType.ClientUserToggleBlockNonFriendPm)]
public class UserToggleBlockNonFriendPmHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var isToggle = new BanchoInt(packet.Data);
        session.Attributes.IgnoreNonFriendPm = isToggle.Value == 1;
        return Task.CompletedTask;
    }
}