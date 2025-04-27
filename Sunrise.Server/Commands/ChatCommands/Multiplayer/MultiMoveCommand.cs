using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("move", "mp", isGlobal: true)]
public class MultiMoveCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
        {
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");
        }

        if (session.Match.HasHostPrivileges(session) == false)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used by the host of the room.");
            return;
        }

        if (args == null || args.Length < 2)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp move <username> <slot>");
            return;
        }

        var slot = args[^1];
        var username = string.Join(" ", args[..^1]);

        if (int.TryParse(slot, out var slotNumber) == false)
        {
            session.SendChannelMessage(channel.Name, "Invalid slot.");
            return;
        }

        if (slotNumber is < 1 or > 16)
        {
            session.SendChannelMessage(channel.Name, "Slot must be between 1 and 16.");
            return;
        }
        
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        
        var userToMove = await database.Users.GetUser(username: username);
        if (userToMove == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return;
        }

        var targetSession = session.Match.Players.Values.FirstOrDefault(x => x.UserId == userToMove.Id);
        if (targetSession == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return;
        }

        session.Match.MovePlayer(targetSession, slotNumber - 1, true);

        session.SendChannelMessage(channel.Name, $"{username} has been moved to slot {slotNumber}.");
    }
}