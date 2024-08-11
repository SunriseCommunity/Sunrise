using System.Reflection;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Repositories.Chat;

public static class CommandRepository
{
    private static readonly Dictionary<string, IChatCommand> Handlers = new();

    public static async Task HandleCommand(string message, Session session)
    {
        if (Handlers.Count == 0)
        {
            GetHandlers();
        }

        string? command;
        string[]? args;

        if (message.StartsWith("ACTION"))
        {
            (command, args) = ActionToCommand(message);

            if (command == null)
            {
                return;
            }
        }
        else
        {
            command = message.Split(' ')[0][Configuration.BotPrefix.Length..].ToLower();
            args = message.Split(' ').Skip(1).ToArray();
        }

        var handler = GetHandler(command);

        if (handler == null)
        {
            SendMessage(session, $"Command {command} not found. Type {Configuration.BotPrefix}help for a list of available commands.");
            return;
        }

        await handler.Handle(session, args);
    }

    public static string[] GetCurrentCommands()
    {
        return Handlers.Keys.ToArray();
    }

    public static void SendMessage(Session session, string message)
    {
        session.WritePacket(PacketType.ServerChatMessage,
            new BanchoChatMessage
            {
                Message = message,
                Sender = Configuration.BotUsername,
                Channel = Configuration.BotUsername
            });
    }

    private static (string?, string[]?) ActionToCommand(string message)
    {
        var action = message.Split(' ', 2).Length >= 2 ? message.Split(' ', 2)[1] : null;

        if ((bool)action?.StartsWith("is listening to") || (bool)action?.StartsWith("is watching"))
        {
            var beatmapId = int.TryParse(message.Split('/')[5].Split(' ')[0] ?? string.Empty, out var id) ? id : 0;
            return ("beatmap", [beatmapId.ToString()]);
        }

        return (null, null);
    }

    public static void GetHandlers()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes().Where(x => x.GetInterfaces().Contains(typeof(IChatCommand)));

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type) as IChatCommand;
            var attribute = type.GetCustomAttribute<ChatCommandAttribute>();

            if (attribute == null || instance == null)
            {
                continue;
            }

            Handlers.Add(attribute.Command, instance);
        }
    }

    private static IChatCommand? GetHandler(string command)
    {
        return Handlers.GetValueOrDefault(command);
    }
}