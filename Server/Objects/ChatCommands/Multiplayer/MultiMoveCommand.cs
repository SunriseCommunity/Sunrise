using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects.ChatCommands.Multiplayer;

[ChatCommand("move", "mp", isGlobal: true)]
public class MultiMoveCommand : IChatCommand
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

        if (args == null || args.Length < 2)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp move <username> <slot>");
            return Task.CompletedTask;
        }

        var username = args[0];
        var slot = args[1];

        if (int.TryParse(slot, out var slotNumber) == false)
        {
            session.SendChannelMessage(channel.Name, "Invalid slot.");
            return Task.CompletedTask;
        }

        if (slotNumber is < 1 or > 16)
        {
            session.SendChannelMessage(channel.Name, "Slot must be between 1 and 16.");
            return Task.CompletedTask;
        }

        var targetSession = session.Match.Players.Values.FirstOrDefault(x => x.User.Username == args[0]);

        if (targetSession == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return Task.CompletedTask;
        }

        session.Match.MovePlayer(targetSession, slotNumber - 1, true);

        session.SendChannelMessage(channel.Name, $"{username} has been moved to slot {slotNumber}.");
        return Task.CompletedTask;
    }
}