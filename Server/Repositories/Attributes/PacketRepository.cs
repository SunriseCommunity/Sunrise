using System.Reflection;
using HOPEless.Bancho;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Repositories.Attributes;

public class PacketRepository
{
    private static readonly Dictionary<PacketType, PacketHandler> Handlers = new();
    private static readonly ILogger<PacketRepository> Logger;

    static PacketRepository()
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        Logger = loggerFactory.CreateLogger<PacketRepository>();
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
                $"{DateTime.Now} | User {session.User.Username} (Id: {session.User.Id}) send {packet.Type}");

        SunriseMetrics.PacketHandlingCounterInc(packet, session);

        await handler.Handle(packet, session);
    }

    public static void GetHandlers()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes().Where(x => x.GetInterfaces().Contains(typeof(IHandler)));

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type) as IHandler;
            var attribute = type.GetCustomAttribute<PacketHandlerAttribute>();

            if (attribute == null || instance == null) continue;

            var handler = new PacketHandler(instance, attribute.SuppressLogger);
            Handlers.Add(attribute.PacketType, handler);
        }
    }

    private static PacketHandler? GetHandler(PacketType type)
    {
        return Handlers.GetValueOrDefault(type);
    }
}