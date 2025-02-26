﻿using HOPEless.Bancho;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Services;

public static class BanchoService
{
    public static async Task ProcessPackets(Session session, MemoryStream buffer, ILogger logger)
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
                await PacketRepository.HandlePacket(packet, session);
            }
        }
        catch (Exception e)
        {
            var errorMessage = $"Failed to process Bancho packet: {e.Message}";
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.BanchoProcess, session, errorMessage);
            logger.LogError(e, errorMessage);
        }
    }

    public static async Task<string?> GetFriends(string username)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var user = await database.UserService.GetUser(username: username);

        if (user == null)
            return null;

        var friends = user.FriendsList;

        return string.Join("\n", friends);
    }

    public static string GetCurrentEventJson()
    {
        var eventImageUri = $"https://assets.{Configuration.Domain}/events/EventBanner.jpg";

        var json =
            """{ "images": [{ "image": "{img}", "url": "https://github.com/SunriseCommunity/Sunrise", "IsCurrent": true, "begins": null, "expires": "2099-06-01T12:00:00+00:00"}] }""";
        json = json.Replace("{img}", eventImageUri);

        return json;
    }
}