using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Session;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientFriendsRemove)]
public class FriendsRemoveHandler : IPacketHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        var friendId = new BanchoInt(packet.Data);

        session.User.RemoveFriend(friendId.Value);

        await ServicesProviderHolder.GetRequiredService<DatabaseManager>().UserService.UpdateUser(session.User);
    }
}