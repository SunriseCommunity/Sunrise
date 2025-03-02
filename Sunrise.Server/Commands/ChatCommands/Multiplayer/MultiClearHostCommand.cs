using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("clearhost", "mp", isGlobal: true)]
public class MultiClearHostCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
        {
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");
        }

        if (session.Match.HasHostPrivileges(session, true) == false)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used by the owner of the room.");
            return Task.CompletedTask;
        }

        session.Match.ClearHost();

        session.SendChannelMessage(channel.Name, "Host has been cleared. Feel free to use !mp host <username> to set a new host.");

        return Task.CompletedTask;
    }
}