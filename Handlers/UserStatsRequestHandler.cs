using HOPEless.Bancho;
using osu.Shared.Serialization;
using Sunrise.Objects;
using Sunrise.Services;

namespace Sunrise.Handlers;

public class UserStatsRequestHandler : IHandler
{
    public void Handle(BanchoPacket packet, Player player, BanchoService bancho, PlayerRepository repository)
    {
        var msa = new MemoryStream(packet.Data);
        var reader = new SerializationReader(msa);
                        
        var ids = new List<int>();

        int length = reader.ReadInt16();
        for (var i = 0; i < length; i++) 
            ids.Add(reader.ReadInt32());
                        
        foreach (var value in ids.Where(repository.ContainsPlayer))
        {
            bancho.SendUserStats(repository.GetPlayer(value));
        }
    }
    
    
}