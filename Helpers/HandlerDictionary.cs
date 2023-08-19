using HOPEless.Bancho;
using Sunrise.Handlers;

namespace Sunrise.Helpers;

public static class HandlerDictionary
{
    private static Dictionary<PacketType, IHandler>? _handlers;
    public static Dictionary<PacketType, IHandler> Handlers => _handlers ??= GetDictionary();

    private static Dictionary<PacketType, IHandler> GetDictionary()
    {
        return new Dictionary<PacketType, IHandler>
        {
            { PacketType.ClientStatusRequestOwn, new StatusRequestOwnHandler() },
            { PacketType.ClientDisconnect, new DisconnectHandler() },
            { PacketType.ClientUserStatsRequest, new UserStatsRequestHandler() },
            { PacketType.ClientUserStatus, new UserStatusHandler() },
            { PacketType.ClientPong, new PongHandler() }
        };
    }
}