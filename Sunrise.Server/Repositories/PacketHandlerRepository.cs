using System.Reflection;
using HOPEless.Bancho;
using Serilog;
using Sunrise.Server.Attributes;
using Sunrise.Server.Packets;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using ILogger = Serilog.ILogger;

namespace Sunrise.Server.Repositories;

public static class PacketHandlerRepository
{
    private static readonly Dictionary<PacketType, PacketHandler> Handlers = new();
    private static readonly ILogger Logger = Log.ForContext(typeof(PacketHandlerRepository));

    public static async Task HandlePacket(BanchoPacket packet, Session session)
    {
        if (Handlers.Count == 0) GetHandlers();

        var handler = GetHandler(packet.Type);

        if (handler == null)
        {
            Logger.Error("No handler found for packet {packetType}", packet.Type);
            return;
        }

        if (!handler.SuppressLogging)
            Logger.Information(
                "User (Id: {userId}) sent {packetType}",
                session.UserId,
                packet.Type);

        SunriseMetrics.PacketHandlingCounterInc(packet, session);

        await handler.Handle(packet, session);
    }

    public static void GetHandlers()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes().Where(x => x.GetInterfaces().Contains(typeof(IPacketHandler)));

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type) as IPacketHandler;
            var attribute = type.GetCustomAttribute<PacketHandlerAttribute>();

            if (attribute == null || instance == null) continue;

            var handler = new PacketHandler(instance, attribute.SuppressLogger);
            Handlers.TryAdd(attribute.PacketType, handler);
        }
    }

    private static PacketHandler? GetHandler(PacketType type)
    {
        return Handlers.GetValueOrDefault(type);
    }
}