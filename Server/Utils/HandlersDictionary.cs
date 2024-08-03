using HOPEless.Bancho;
using Sunrise.Server.Services.Handlers.Client;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Utils;

public static class HandlersDictionary
{
    private static Dictionary<PacketType, IHandler>? _handlers;

    private static List<PacketType>? _suppressed;
    public static Dictionary<PacketType, IHandler> Handlers => _handlers ??= GetDictionary();
    public static List<PacketType> Suppressed => _suppressed ??= GetSuppressed();

    private static List<PacketType> GetSuppressed()
    {
        return
        [
            PacketType.ClientPong,
            PacketType.ClientRequestPlayerList
        ];
    }

    private static Dictionary<PacketType, IHandler> GetDictionary()
    {
        return new Dictionary<PacketType, IHandler>
        {
            {
                PacketType.ClientUserPresenceRequest, new UserPresenceRequestHandler()
            },
            {
                PacketType.ClientStatusRequestOwn, new StatusRequestOwnHandler()
            },
            {
                PacketType.ClientDisconnect, new DisconnectHandler()
            },
            {
                PacketType.ClientUserStatsRequest, new UserStatsRequestHandler()
            },
            {
                PacketType.ClientUserStatus, new UserStatusHandler()
            },
            {
                PacketType.ClientPong, new PongHandler()
            }
        };
    }
}