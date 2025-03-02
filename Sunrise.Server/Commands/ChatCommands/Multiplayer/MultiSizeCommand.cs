using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("size", "mp", isGlobal: true)]
public class MultiSizeCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
        {
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");
        }

        if (session.Match.HasHostPrivileges(session) == false)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used by the host of the room.");
            return Task.CompletedTask;
        }

        if (args == null || args.Length == 0)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp size <size>");
            return Task.CompletedTask;
        }

        if (int.TryParse(args[0], out var size) == false)
        {
            session.SendChannelMessage(channel.Name, "Invalid size.");
            return Task.CompletedTask;
        }

        if (size is < 1 or > 16)
        {
            session.SendChannelMessage(channel.Name, "Size must be between 1 and 16.");
            return Task.CompletedTask;
        }

        for (var i = 0; i < 16; i++)
        {
            session.Match.UpdateLock(i, i < size);
        }

        session.SendChannelMessage(channel.Name, "Room size has been updated.");
        return Task.CompletedTask;
    }
}