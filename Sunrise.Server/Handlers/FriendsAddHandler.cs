using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientFriendsAdd)]
public class FriendsAddHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        var friendId = new BanchoInt(packet.Data);

        session.User.AddFriend(friendId.Value);

        await ServicesProviderHolder.GetRequiredService<DatabaseManager>().UserService.UpdateUser(session.User);
    }
}