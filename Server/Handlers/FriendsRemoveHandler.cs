using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientFriendsRemove)]
public class FriendsRemoveHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        var friendId = new BanchoInt(packet.Data);

        session.User.RemoveFriend(friendId.Value);

        await ServicesProviderHolder.GetRequiredService<SunriseDb>().UpdateUser(session.User);
    }
}