using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects.ChatCommands.Multiplayer;

[ChatCommand("kick", "mp", isGlobal: true)]
public class MultiKickCommand : IChatCommand
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
            session.SendChannelMessage(channel.Name, "Usage: !mp kick <username>");
            return Task.CompletedTask;
        }

        var user = session.Match.Players.Values.FirstOrDefault(x => x.User.Username == args[0]);

        if (user == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return Task.CompletedTask;
        }

        session.Match.RemovePlayer(user, true);

        return Task.CompletedTask;
    }
}