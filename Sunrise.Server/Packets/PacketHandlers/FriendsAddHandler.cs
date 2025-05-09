using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientFriendsAdd)]
public class FriendsAddHandler : IPacketHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        var friendId = new BanchoInt(packet.Data);

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var relationship = await database.Users.Relationship.GetUserRelationship(session.UserId, friendId.Value);
        if (relationship == null)
            return;

        relationship.Relation = UserRelation.Friend;

        await database.Users.Relationship.UpdateUserRelationship(relationship);
    }
}