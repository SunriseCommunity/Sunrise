using HOPEless.Bancho;
using Prometheus;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Utils;

public static class SunriseMetrics
{
    private static readonly Counter PacketHandlingCounter = Metrics.CreateCounter(
        "bancho_packets_handled_total",
        "Counts the total number of handled packets",
        new CounterConfiguration
        {
            LabelNames = ["packet_type", "username", "user_id"]
        });

    private static readonly Counter ExternalApiRequestsCounter = Metrics.CreateCounter(
        "external_api_requests_total",
        "Counts the total number of external API requests",
        new CounterConfiguration
        {
            LabelNames = ["api_type", "api_server", "username", "user_id"]
        });

    public static void PacketHandlingCounterInc(BanchoPacket packet, Session session)
    {
        PacketHandlingCounter.WithLabels(
            packet.Type.ToString(),
            session.User.Username,
            session.User.Id.ToString()
        ).Inc();
    }

    public static void ExternalApiRequestsCounterInc(ApiType type, ApiServer server, Session session)
    {
        ExternalApiRequestsCounter.WithLabels(
            type.ToString(),
            server.ToString(),
            session.User.Username,
            session.User.Id.ToString()
        ).Inc();
    }
}