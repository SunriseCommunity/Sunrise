using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Data;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
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

        await ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>().UpdateUser(session.User);
    }
}