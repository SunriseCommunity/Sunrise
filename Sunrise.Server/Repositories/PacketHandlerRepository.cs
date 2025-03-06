using System.Reflection;
using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Server.Packets;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Repositories;

public class PacketHandlerRepository
{
    private static readonly Dictionary<PacketType, PacketHandler> Handlers = new();
    private static readonly ILogger<PacketHandlerRepository> Logger;

    static PacketHandlerRepository()
    {
        using var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        Logger = loggerFactory.CreateLogger<PacketHandlerRepository>();
    }

    public static async Task HandlePacket(BanchoPacket packet, Session session)
    {
        if (Handlers.Count == 0) GetHandlers();

        var handler = GetHandler(packet.Type);

        if (handler == null)
        {
            Logger.LogError($"No handler found for packet {packet.Type}");
            return;
        }

        if (!handler.SuppressLogging)
            Logger.LogInformation(
                $"{DateTime.Now} | User (Id: {session.UserId}) send {packet.Type}");

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