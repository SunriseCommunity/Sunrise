using HOPEless.Bancho;
using Microsoft.Extensions.Logging;
using Prometheus;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Application;

public static class SunriseMetrics
{
    private static readonly Counter PacketHandlingCounter = Metrics.CreateCounter(
        "bancho_packets_handled_total",
        "Counts the total number of handled packets",
        new CounterConfiguration
        {
            LabelNames = ["packet_type", "user_id"]
        });

    private static readonly Counter ExternalApiRequestsCounter = Metrics.CreateCounter(
        "external_api_requests_total",
        "Counts the total number of external API requests",
        new CounterConfiguration
        {
            LabelNames = ["api_type", "api_server", "user_id"]
        });

    private static readonly Counter RequestReturnedErrorCounter = Metrics.CreateCounter(
        "request_returned_error_total",
        "Counts the total number of requests that returned an error",
        new CounterConfiguration
        {
            LabelNames = ["request_type", "user_id", "error_message"]
        });

    public static void PacketHandlingCounterInc(BanchoPacket packet, Session session)
    {
        PacketHandlingCounter.WithLabels(
            packet.Type.ToString(),
            session.UserId.ToString()
        ).Inc();
    }

    public static void ExternalApiRequestsCounterInc(ApiType type, ApiServer server, BaseSession session)
    {
        ExternalApiRequestsCounter.WithLabels(
            type.ToString(),
            server.ToString(),
            session.UserId.ToString()
        ).Inc();
    }

    public static void RequestReturnedErrorCounterInc(string requestType, Session? session, string? errorMessage)
    {
        if (!requestType.IsValidRequestType())
        {
            using var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
            var logger = loggerFactory.CreateLogger(string.Empty);
            logger.LogCritical($"Invalid request type: {requestType} in Metrics.RequestReturnedErrorCounterInc");
            return;
        }

        RequestReturnedErrorCounter.WithLabels(
            requestType,
            session?.UserId.ToString() ?? "-1",
            errorMessage ?? "Not specified"
        ).Inc();
    }
}