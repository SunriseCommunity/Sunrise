using HOPEless.Bancho;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Services;

public class BanchoService
{
    public async Task ProcessPackets(Session session, MemoryStream buffer, ILogger logger)
    {
        try
        {
            var packets = BanchoSerializer.DeserializePackets(buffer).ToList();

            // Note: In theory if user upon login still has a pending disconnect packet, we should ignore it.
            // Afraid this running this every time might cause issues, need to investigate.
            if (packets.Any(p => p.Type is PacketType.ClientDisconnect) && packets.Any(p => p.Type is PacketType.ClientStatusRequestOwn))
                packets = packets.Where(p => p.Type != PacketType.ClientDisconnect).ToList();

            foreach (var packet in packets)
            {
                await PacketHandlerRepository.HandlePacket(packet, session);
            }
        }
        catch (Exception e)
        {
            var errorMessage = $"Failed to process Bancho packet: {e.Message}";
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.BanchoProcess, session, errorMessage);
            logger.LogError(e, errorMessage);
        }
    }

    public string GetCurrentEventJson()
    {
        var eventImageUri = $"https://assets.{Configuration.Domain}/events/EventBanner.jpg";

        var json =
            """{ "images": [{ "image": "{img}", "url": "https://github.com/SunriseCommunity/Sunrise", "IsCurrent": true, "begins": null, "expires": "2099-06-01T12:00:00+00:00"}] }""";
        json = json.Replace("{img}", eventImageUri);

        return json;
    }
}