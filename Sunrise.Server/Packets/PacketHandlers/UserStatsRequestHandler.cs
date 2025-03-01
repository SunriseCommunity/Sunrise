using HOPEless.Bancho;
using osu.Shared.Serialization;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientUserStatsRequest, true)]
public class UserStatsRequestHandler : IPacketHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        var msa = new MemoryStream(packet.Data);
        using var reader = new SerializationReader(msa);

        var ids = new List<int>();

        int length = reader.ReadInt16();

        for (var i = 0; i < length; i++)
        {
            ids.Add(reader.ReadInt32());
        }

        ids.Remove(session.UserId);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var player in ids.Select(id => sessions.GetSession(userId: id)))
        {
            if (player != null)
                session.WritePacket(PacketType.ServerUserData, await player.Attributes.GetPlayerData());
        }
    }
}