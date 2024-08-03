using HOPEless.Bancho;
using osu.Shared.Serialization;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services.Handlers.Client;

public class UserStatsRequestHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        var msa = new MemoryStream(packet.Data);
        var reader = new SerializationReader(msa);

        var ids = new List<int>();

        int length = reader.ReadInt16();
        for (var i = 0; i < length; i++)
            ids.Add(reader.ReadInt32());

        ids.Remove(session.User.Id);

        foreach (var player in ids.Select(id => services.Sessions.GetSessionByUserId(id)))
            if (player != null)
            {
                session.WritePacket(PacketType.ServerUserData, await player.Attributes.GetPlayerData());
            }
    }
}

