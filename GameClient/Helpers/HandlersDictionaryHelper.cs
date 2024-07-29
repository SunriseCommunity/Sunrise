using HOPEless.Bancho;
using Sunrise.GameClient.Handlers;
using Sunrise.GameClient.Types.Interfaces;

namespace Sunrise.GameClient.Helpers;

public static class HandlersDictionaryHelper
{
    private static Dictionary<PacketType, IHandler>? _handlers;

    private static List<PacketType>? _suppressed;
    public static Dictionary<PacketType, IHandler> Handlers => _handlers ??= GetDictionary();

    public static List<PacketType> Suppressed => _suppressed ??= GetSuppressed();

    private static List<PacketType> GetSuppressed()
    {
        return new List<PacketType>
        {
            PacketType.ClientPong,
            PacketType.ClientRequestPlayerList,
        };
    }

    private static Dictionary<PacketType, IHandler> GetDictionary()
    {
        return new Dictionary<PacketType, IHandler>
            { 
            { PacketType.ClientUserPresenceRequest, new ClientUserPresenceRequest() },
            { PacketType.ClientStatusRequestOwn, new StatusRequestOwnHandler() },
            { PacketType.ClientDisconnect, new DisconnectHandler() },
            { PacketType.ClientUserStatsRequest, new UserStatsRequestHandler() },
            { PacketType.ClientUserStatus, new UserStatusHandler() },
            { PacketType.ClientPong, new PongHandler() },
  };
    }
}

