using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("aborttimer", "mp", isGlobal: true)]
public class MultiAbortTimerCommand : IChatCommand
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

        if (session.Match.HasActiveTimer() == false)
        {
            session.SendChannelMessage(channel.Name, "There is no timer to abort.");
            return Task.CompletedTask;
        }

        session.Match.StopTimer();

        foreach (var player in session.Match.Players.Values)
        {
            player.SendChannelMessage(channel.Name, "Timer has been aborted.");
        }

        return Task.CompletedTask;
    }
}