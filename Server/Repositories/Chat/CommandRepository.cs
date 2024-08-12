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
    private static readonly Dictionary<string, ChatCommand> Handlers = new();

    public static async Task HandleCommand(BanchoChatMessage message, Session session)
    {
        if (Handlers.Count == 0)
        {
            GetHandlers();
        }

        string? command;
        string[]? args;

        if (message.Message.StartsWith("ACTION") && message.Channel == Configuration.BotUsername)
        {
            (command, args) = ActionToCommand(message.Message);

            if (command == null)
            {
                return;
            }
        }
        else
        {
            command = message.Message.Split(' ')[0][Configuration.BotPrefix.Length..].ToLower();
            args = message.Message.Split(' ').Skip(1).ToArray();
        }

        var handler = GetHandler(command);

        switch (handler)
        {
            case null or { IsGlobal: false } when message.Channel != Configuration.BotUsername:
                return;
            case null:
                SendMessage(session, $"Command {command} not found. Type {Configuration.BotPrefix}help for a list of available commands.");
                return;
        }

        if (handler.RequiredPrivileges > session.User.Privilege)
        {
            SendMessage(session, "You don't have permission to use this command.");
            return;
        }

        ChatChannel? channel = null;

        if (handler.IsGlobal && message.Channel != Configuration.BotUsername)
        {
            var channels = ServicesProviderHolder.ServiceProvider.GetRequiredService<ChannelRepository>();
            channel = channels.GetChannel(message.Channel);
        }

        await handler.Handle(session, channel, args);
    }

    public static string[] GetAvailableCommands(Session session)
    {
        var privilege = session.User.Privilege;

        return Handlers
            .Where(x => x.Value.RequiredPrivileges <= privilege)
            .Select(x => x.Key)
            .ToArray();
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

        if (action?.StartsWith("is listening to") == true || action?.StartsWith("is watching") == true)
        {
            if (message.Split('/').Length < 6)
            {
                return (null, null);
            }

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

            var command = new ChatCommand(instance, attribute.RequiredRank, attribute.IsGlobal);
            Handlers.Add(attribute.Command, command);
        }
    }

    private static ChatCommand? GetHandler(string command)
    {
        return Handlers.GetValueOrDefault(command);
    }
}