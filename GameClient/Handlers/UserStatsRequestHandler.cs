using HOPEless.Bancho;
using osu.Shared.Serialization;
using Sunrise.GameClient.Objects;
using Sunrise.GameClient.Types.Interfaces;

namespace Sunrise.GameClient.Handlers;

public class UserStatsRequestHandler : IHandler
{
    public void Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        var msa = new MemoryStream(packet.Data);
        var reader = new SerializationReader(msa);

        var ids = new List<int>();

        int length = reader.ReadInt16();
        for (var i = 0; i < length; i++)
            ids.Add(reader.ReadInt32());

        foreach (var player in ids.Select(id => services.Sessions.GetSessionByUserId(id)))
            if (player != null)
                session.WritePacket(PacketType.ServerUserData, player.Attributes.GetPlayerData());
    }
}

