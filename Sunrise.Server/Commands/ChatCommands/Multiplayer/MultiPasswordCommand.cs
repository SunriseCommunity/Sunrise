using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("password", "mp", isGlobal: true)]
public class MultiPasswordCommand : IChatCommand
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
            session.Match.ChangePassword(string.Empty);
        }
        else
        {
            session.Match.ChangePassword(string.Join("_", args));
        }

        session.SendChannelMessage(channel.Name, "Password has been updated.");
        return Task.CompletedTask;
    }
}