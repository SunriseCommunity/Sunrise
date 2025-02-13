using HOPEless.Bancho;
using osu.Shared.Serialization;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientUserStatsRequest, true)]
public class UserStatsRequestHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        var msa = new MemoryStream(packet.Data);
        using var reader = new SerializationReader(msa);

        var ids = new List<int>();

        int length = reader.ReadInt16();

        for (var i = 0; i < length; i++) ids.Add(reader.ReadInt32());

        ids.Remove(session.User.Id);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var player in ids.Select(id => sessions.GetSession(userId: id)))
            if (player != null)
                session.WritePacket(PacketType.ServerUserData, await player.Attributes.GetPlayerData());
    }
}