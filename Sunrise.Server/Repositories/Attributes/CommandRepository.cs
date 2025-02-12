using System.Reflection;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Chat;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Repositories.Attributes;

public static class CommandRepository
{
    private static readonly Dictionary<string, ChatCommand> Handlers = new();
    private static string[] Prefixes { get; set; } = [];

    public static async Task HandleCommand(BanchoChatMessage message, Session session)
    {
        if (Handlers.Count == 0) GetHandlers();

        string? command;
        string[]? args;

        if (message.Message.StartsWith("ACTION") && message.Channel == Configuration.BotUsername)
        {
            (command, args) = ActionToCommand(session, message.Message);

            if (command == null) return;
        }
        else
        {
            command = message.Message.Split(' ')[0][Configuration.BotPrefix.Length..].ToLower();
            args = message.Message.Split(' ').Skip(1).ToArray();
        }

        if (args != null && Prefixes.Contains(command) && args.Length > 0)
        {
            command = $"{command} {args[0]}";
            args = args.Skip(1).ToArray();
        }

        var handler = GetHandler(command);

        switch (handler)
        {
            case null or { IsGlobal: false } when message.Channel != Configuration.BotUsername:
                return;
            case null:
                SendMessage(session,
                    $"Command {command} not found. Type {Configuration.BotPrefix}help for a list of available commands.");
                return;
        }

        if (!session.User.Privilege.HasFlag(handler.RequiredPrivileges))
        {
            SendMessage(session, "You don't have permission to use this command.");
            return;
        }

        ChatChannel? channel = null;

        if (handler.IsGlobal && message.Channel != Configuration.BotUsername)
        {
            var channels = ServicesProviderHolder.GetRequiredService<ChannelRepository>();
            channel = channels.GetChannel(session, message.Channel);
        }

        if (handler.Prefix != string.Empty && HasPrefixException(session, message, handler, channel))
            return;

        await handler.Handle(session, channel, args);
    }

    public static string[] GetAvailableCommands(Session session)
    {
        var privilege = session.User.Privilege;

        return Handlers
            .Where(x => privilege.HasFlag(x.Value.RequiredPrivileges))
            .Select(x => x.Key)
            .ToArray();
    }

    public static void SendMessage(Session session, string message, string? channel = null)
    {
        session.WritePacket(PacketType.ServerChatMessage,
            new BanchoChatMessage
            {
                Message = message,
                Sender = Configuration.BotUsername,
                Channel = channel ?? Configuration.BotUsername
            });
    }

    public static void TrySendMessage(int userId, string message, string? channel = null)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var session = sessions.GetSession(userId: userId);

        session?.WritePacket(PacketType.ServerChatMessage,
            new BanchoChatMessage
            {
                Message = message,
                Sender = Configuration.BotUsername,
                Channel = channel ?? Configuration.BotUsername
            });
    }

    private static bool HasPrefixException(Session session, BanchoChatMessage message, ChatCommand handler,
        ChatChannel? channel)
    {
        if (handler.Prefix == "mp")
        {
            if (session.Match == null)
            {
                if (!handler.IsGlobal || message.Channel == Configuration.BotUsername)
                    SendMessage(session, "You must be in a multiplayer lobby to use this command.");
                return true;
            }

            if (channel?.Name.StartsWith("#multiplayer") == false)
            {
                if (!handler.IsGlobal || message.Channel == Configuration.BotUsername)
                    SendMessage(session, "You must be in the multiplayer channel to use this command.");
                return true;
            }
        }

        return false;
    }

    private static (string?, string[]?) ActionToCommand(Session session, string message)
    {
        var action = message.Split(' ', 2).Length >= 2 ? message.Split(' ', 2)[1] : null;

        var beatmapsAction = new[]
        {
            "is listening to",
            "is watching",
            "is playing"
        };

        if (beatmapsAction.Any(x => action?.StartsWith(x) == true))
        {
            if (message.Split('/').Length < 6) return (null, null);

            var beatmapId = int.TryParse(message.Split('/')[5].Split(' ')[0], out var id) ? id : 0;

            var mods = action?.StartsWith("is watching") == true
                ? session.Spectating?.Attributes.Status.CurrentMods
                : Mods.None;

            return ("beatmap", [beatmapId.ToString(), mods?.GetModsString() ?? string.Empty]);
        }

        return (null, null);
    }

    public static void GetHandlers()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(x => x.GetInterfaces().Contains(typeof(IChatCommand)));

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type) as IChatCommand;
            var attribute = type.GetCustomAttribute<ChatCommandAttribute>();

            if (attribute == null || instance == null) continue;

            if (attribute.Prefix != string.Empty) Prefixes = Prefixes.Append(attribute.Prefix).ToArray();

            var command = new ChatCommand(instance, attribute.Prefix, attribute.RequiredPrivileges, attribute.IsGlobal);
            Handlers.TryAdd($"{attribute.Prefix} {attribute.Command}".Trim(),
                command); // .Trim() => https://imgur.com/a/0rsYZRv
        }
    }

    private static ChatCommand? GetHandler(string command)
    {
        return Handlers.GetValueOrDefault(command);
    }
}