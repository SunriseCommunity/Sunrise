using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("host", "mp", isGlobal: true)]
public class MultiHostCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");

        if (session.Match.HasHostPrivileges(session) == false)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used by the host of the room.");
            return Task.CompletedTask;
        }

        if (args == null || args.Length == 0)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp host <username>");
            return Task.CompletedTask;
        }
        
        var username = string.Join(" ", args[0..]);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var targetSession = sessions.GetSession(username);

        if (targetSession == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return Task.CompletedTask;
        }

        var targetSlot = session.Match.Slots.FirstOrDefault(x => x.Value.UserId == targetSession.UserId);

        if (targetSlot.Value == null)
        {
            session.SendChannelMessage(channel.Name, "User is not in the room.");
            return Task.CompletedTask;
        }

        session.Match.TransferHost(targetSlot.Key);

        session.SendChannelMessage(channel.Name, $"Host has been transferred to {username}.");

        return Task.CompletedTask;
    }
}