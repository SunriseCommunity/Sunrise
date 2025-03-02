using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientFriendsRemove)]
public class FriendsRemoveHandler : IPacketHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        var friendId = new BanchoInt(packet.Data);

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                
        var user = await database.Users.GetUser(session.UserId);
        if (user == null)
            return;

        user.RemoveFriend(friendId.Value);

        await database.Users.UpdateUser(user);
    }
}