using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("unlock", "mp", isGlobal: true)]
public class MultiUnlockCommand : IChatCommand
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

        session.Match.Locked = false;

        session.SendChannelMessage(channel.Name, "Room has been unlocked. Players can change their teams or slots again.");

        return Task.CompletedTask;
    }
}